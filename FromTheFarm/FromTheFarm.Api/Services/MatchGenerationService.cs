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
