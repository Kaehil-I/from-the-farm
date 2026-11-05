using FromTheFarm.Api.Models;

namespace FromTheFarm.Api.Services;

// Creates match documents for listing/demand pairs and tells the other party
// when a pair is new. Used both when someone opens their feed and, so that the
// counterpart hears about it straight away, when a listing or demand is posted.
//
// Match ids are deterministic (listingId:demandRequestId), so running the same
// pair twice upserts rather than duplicates.
public class MatchGenerationService
{
    private readonly IMongoRepository<Listing> _listings;
    private readonly IMongoRepository<DemandRequest> _demands;
    private readonly IMongoRepository<MatchDocument> _matches;
    private readonly MatchingService _matchingService;
    private readonly NotificationService _notifications;
    private readonly ILogger<MatchGenerationService> _logger;

    public MatchGenerationService(
        IMongoRepository<Listing> listings,
        IMongoRepository<DemandRequest> demands,
        IMongoRepository<MatchDocument> matches,
        MatchingService matchingService,
        NotificationService notifications,
        ILogger<MatchGenerationService> logger)
    {
        _listings = listings;
        _demands = demands;
        _matches = matches;
        _matchingService = matchingService;
        _notifications = notifications;
        _logger = logger;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // Whether a pair is still worth showing. A deleted listing, a closed or
    // expired demand, or a user matched with their own records (possible after
    // switching role) has no business in anyone's feed.
    public static bool IsStillAvailable(Listing? listing, DemandRequest? demand) =>
        listing is { Status: "Active" }
        && demand is { Status: "Open" }
        && demand.Deadline >= Today
        && listing.FarmerId != demand.BuyerId
        && string.Equals(listing.CropType, demand.CropType, StringComparison.OrdinalIgnoreCase);

    public async Task<bool> IsStillAvailableAsync(MatchDocument match) =>
        IsStillAvailable(
            await _listings.GetByIdAsync(match.ListingId),
            await _demands.GetByIdAsync(match.DemandRequestId));

    // Removes suggestions whose listing or demand has since gone away and returns
    // the matches that remain. Only Suggested matches are dropped: a Confirmed or
    // Completed one records an exchange that was agreed or happened, and stays
    // as history even if the listing behind it is later deleted.
    public async Task<List<MatchDocument>> DropStaleSuggestionsAsync(List<MatchDocument> matches)
    {
        var suggested = matches.Where(m => m.Status == "Suggested").ToList();
        if (suggested.Count == 0)
        {
            return matches;
        }

        var listingIds = suggested.Select(m => m.ListingId).Distinct().ToList();
        var demandIds = suggested.Select(m => m.DemandRequestId).Distinct().ToList();
        var listings = (await _listings.FindAsync(l => listingIds.Contains(l.Id))).ToDictionary(l => l.Id);
        var demands = (await _demands.FindAsync(d => demandIds.Contains(d.Id))).ToDictionary(d => d.Id);

        var kept = new List<MatchDocument>();
        foreach (var match in matches)
        {
            if (match.Status != "Suggested")
            {
                kept.Add(match);
                continue;
            }

            listings.TryGetValue(match.ListingId, out var listing);
            demands.TryGetValue(match.DemandRequestId, out var demand);

            if (IsStillAvailable(listing, demand))
            {
                kept.Add(match);
            }
            else
            {
                await _matches.DeleteAsync(match.Id);
            }
        }

        return kept;
    }

    public async Task GenerateForFarmerAsync(string farmerId, int searchRadiusKm)
    {
        var myListings = await _listings.FindAsync(l => l.FarmerId == farmerId && l.Status == "Active");

        foreach (var listing in myListings)
        {
            await GenerateForListingAsync(listing, searchRadiusKm, farmerId);
        }
    }

    public async Task GenerateForBuyerAsync(string buyerId, int searchRadiusKm)
    {
        var myDemands = await _demands.FindAsync(d => d.BuyerId == buyerId && d.Status == "Open");

        foreach (var demand in myDemands)
        {
            await GenerateForDemandAsync(demand, searchRadiusKm, buyerId);
        }
    }

    // Called when a listing is posted. Never throws: a matching problem must not
    // turn a successfully saved listing into a failed request, and the next
    // feed fetch regenerates the matches anyway.
    public async Task OnListingCreatedAsync(Listing listing, int searchRadiusKm)
    {
        try
        {
            await GenerateForListingAsync(listing, searchRadiusKm, listing.FarmerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not match new listing {ListingId}.", listing.Id);
        }
    }

    public async Task OnDemandCreatedAsync(DemandRequest demand, int searchRadiusKm)
    {
        try
        {
            await GenerateForDemandAsync(demand, searchRadiusKm, demand.BuyerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not match new demand {DemandId}.", demand.Id);
        }
    }

    private async Task GenerateForListingAsync(Listing listing, int searchRadiusKm, string requesterId)
    {
        var candidateDemands = await _demands.FindAsync(d => d.CropType == listing.CropType && d.Status == "Open");

        foreach (var demand in candidateDemands)
        {
            await TryUpsertMatchAsync(listing, demand, searchRadiusKm, requesterId);
        }
    }

    private async Task GenerateForDemandAsync(DemandRequest demand, int searchRadiusKm, string requesterId)
    {
        var candidateListings = await _listings.FindAsync(l => l.CropType == demand.CropType && l.Status == "Active");

        foreach (var listing in candidateListings)
        {
            await TryUpsertMatchAsync(listing, demand, searchRadiusKm, requesterId);
        }
    }

    private async Task TryUpsertMatchAsync(Listing listing, DemandRequest demand, int searchRadiusKm, string requesterId)
    {
        if (!IsStillAvailable(listing, demand)) return;

        var score = _matchingService.TryScoreMatch(listing, demand, searchRadiusKm);
        if (score is null) return;

        var deterministicId = $"{listing.Id}:{demand.Id}";
        var existing = await _matches.GetByIdAsync(deterministicId);

        var match = existing ?? new MatchDocument
        {
            Id = deterministicId,
            ListingId = listing.Id,
            DemandRequestId = demand.Id,
            FarmerId = listing.FarmerId,
            BuyerId = demand.BuyerId,
            Status = "Suggested"
        };

        match.Score = score.Value;
        match.CounterpartSnapshot = new CounterpartSnapshot
        {
            CropType = listing.CropType,
            Quantity = listing.Quantity,
            Unit = listing.Unit,
            DistanceKm = MatchingService.CalculateDistanceKm(
                listing.Location.Latitude, listing.Location.Longitude,
                demand.Location.Latitude, demand.Location.Longitude),
            RelevantDate = listing.HarvestDate
        };

        await _matches.UpsertAsync(match);

        // Only a match that did not exist before is news. The feed regenerates
        // matches on every fetch, so notifying on every upsert would repeat the
        // same alert each time the recipient's counterpart opened the app.
        if (existing is null)
        {
            var recipient = match.FarmerId == requesterId ? match.BuyerId : match.FarmerId;
            await _notifications.NotifyAsync(recipient, MatchNotification.NewMatch, match);
        }
    }
}
