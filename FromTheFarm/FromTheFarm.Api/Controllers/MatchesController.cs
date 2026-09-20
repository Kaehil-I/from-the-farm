using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/matches")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly MongoRepository<UserProfile> _users;
    private readonly MongoRepository<Listing> _listings;
    private readonly MongoRepository<DemandRequest> _demands;
    private readonly MongoRepository<MatchDocument> _matches;
    private readonly MatchingService _matchingService;

    public MatchesController(
        MongoRepository<UserProfile> users,
        MongoRepository<Listing> listings,
        MongoRepository<DemandRequest> demands,
        MongoRepository<MatchDocument> matches,
        MatchingService matchingService)
    {
        _users = users;
        _listings = listings;
        _demands = demands;
        _matches = matches;
        _matchingService = matchingService;
    }

    public record MatchFeedItem(string MatchId, double Score, CounterpartSnapshot Counterpart, string Status);
    public record ContactInfo(string? DisplayName, string? Phone);
    public record MatchDetailResponse(string MatchId, double Score, string Status, ContactInfo CounterpartContact);
    public record RatingRequest(bool ThumbsUp);

    // Section 5: user is identified from the auth token, not a path
    // parameter — a deliberate refinement over the worksheet's
    // GET /matches/{userId}, which would let anyone query anyone's feed.
    [HttpGet]
    public async Task<ActionResult<List<MatchFeedItem>>> GetFeed()
    {
        var uid = User.GetFirebaseUid();
        var profile = await _users.GetByIdAsync(uid);
        if (profile?.Role is null)
        {
            return BadRequest("Complete onboarding (set a role) before requesting matches.");
        }

        // Regenerate/refresh candidate matches for this user's own active
        // records. Match IDs are deterministic (listingId:demandRequestId),
        // so repeated calls upsert rather than duplicate.
        //
        // Matching recomputes on every GET. That is acceptable at this
        // scale; a production system would compute matches once at write
        // time, driven by a change stream on Listings and Demands.
        if (profile.Role == "Farmer")
        {
            await GenerateMatchesForFarmerAsync(uid, profile.SearchRadiusKm);
        }
        else
        {
            await GenerateMatchesForBuyerAsync(uid, profile.SearchRadiusKm);
        }

        var feed = _matches.Collection.AsQueryable()
            .Where(m => m.FarmerId == uid || m.BuyerId == uid);

        var results = await feed.ToListAsync();

        var ordered = results
            .OrderByDescending(m => m.Score)
            .Select(m => new MatchFeedItem(m.Id, m.Score, m.CounterpartSnapshot, m.Status))
            .ToList();

        return Ok(ordered);
    }

    [HttpGet("{matchId}")]
    public async Task<ActionResult<MatchDetailResponse>> GetMatchDetail(string matchId)
    {
        var uid = User.GetFirebaseUid();
        var match = await _matches.GetByIdAsync(matchId);

        if (match is null || (match.FarmerId != uid && match.BuyerId != uid))
        {
            return NotFound();
        }

        ContactInfo contact = new(null, null);
        if (match.Status is "Confirmed" or "Completed")
        {
            var counterpartUid = match.FarmerId == uid ? match.BuyerId : match.FarmerId;
            var counterpartProfile = await _users.GetByIdAsync(counterpartUid);
            contact = new ContactInfo(counterpartProfile?.DisplayName, counterpartProfile?.Phone);
        }

        return Ok(new MatchDetailResponse(match.Id, match.Score, match.Status, contact));
    }

    [HttpPost("{matchId}/confirm")]
    public async Task<IActionResult> ConfirmMatch(string matchId)
    {
        var uid = User.GetFirebaseUid();
        var match = await _matches.GetByIdAsync(matchId);

        if (match is null || (match.FarmerId != uid && match.BuyerId != uid))
        {
            return NotFound();
        }

        if (match.Status != "Suggested")
        {
            return BadRequest($"Match must be in 'Suggested' status to confirm — current status is '{match.Status}'.");
        }

        match.Status = "Confirmed";
        match.ConfirmedAt = DateTime.UtcNow;
        await _matches.UpsertAsync(match);

        return NoContent();
    }

    // Performs the Confirmed → Completed transition described in Section 5.
    // Either party marks the exchange as done once it has happened in
    // person, which is what unblocks POST /matches/{matchId}/rating.
    [HttpPost("{matchId}/complete")]
    public async Task<IActionResult> CompleteMatch(string matchId)
    {
        var uid = User.GetFirebaseUid();
        var match = await _matches.GetByIdAsync(matchId);

        if (match is null || (match.FarmerId != uid && match.BuyerId != uid))
        {
            return NotFound();
        }

        if (match.Status != "Confirmed")
        {
            return BadRequest($"Match must be in 'Confirmed' status to complete — current status is '{match.Status}'.");
        }

        match.Status = "Completed";
        match.CompletedAt = DateTime.UtcNow;
        await _matches.UpsertAsync(match);

        return NoContent();
    }

    [HttpPost("{matchId}/rating")]
    public async Task<IActionResult> SubmitRating(string matchId, [FromBody] RatingRequest request, [FromServices] MongoRepository<Rating> ratings)
    {
        var uid = User.GetFirebaseUid();
        var match = await _matches.GetByIdAsync(matchId);

        if (match is null || (match.FarmerId != uid && match.BuyerId != uid))
        {
            return NotFound();
        }

        if (match.Status != "Completed")
        {
            return BadRequest("Ratings can only be submitted once a match is Completed.");
        }

        // One rating per (match, rater) pair — check before inserting.
        var alreadyRated = await ratings.Collection.AsQueryable()
            .AnyAsync(r => r.MatchId == matchId && r.RaisedByUserId == uid);
        if (alreadyRated)
        {
            return Conflict("A rating for this match has already been submitted by this user.");
        }

        var rating = new Rating
        {
            MatchId = matchId,
            RaisedByUserId = uid,
            ThumbsUp = request.ThumbsUp
        };

        await ratings.UpsertAsync(rating);
        return CreatedAtAction(nameof(GetMatchDetail), new { matchId }, null);
    }

    private async Task GenerateMatchesForFarmerAsync(string farmerId, int searchRadiusKm)
    {
        var myListings = await QueryAsync(_listings.Collection.AsQueryable()
            .Where(l => l.FarmerId == farmerId && l.Status == "Active"));

        foreach (var listing in myListings)
        {
            var candidateDemands = await QueryAsync(_demands.Collection.AsQueryable()
                .Where(d => d.CropType == listing.CropType && d.Status == "Open"));

            foreach (var demand in candidateDemands)
            {
                await TryUpsertMatchAsync(listing, demand, searchRadiusKm);
            }
        }
    }

    private async Task GenerateMatchesForBuyerAsync(string buyerId, int searchRadiusKm)
    {
        var myDemands = await QueryAsync(_demands.Collection.AsQueryable()
            .Where(d => d.BuyerId == buyerId && d.Status == "Open"));

        foreach (var demand in myDemands)
        {
            var candidateListings = await QueryAsync(_listings.Collection.AsQueryable()
                .Where(l => l.CropType == demand.CropType && l.Status == "Active"));

            foreach (var listing in candidateListings)
            {
                await TryUpsertMatchAsync(listing, demand, searchRadiusKm);
            }
        }
    }

    private async Task TryUpsertMatchAsync(Listing listing, DemandRequest demand, int searchRadiusKm)
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
    }

    private static async Task<List<T>> QueryAsync<T>(IQueryable<T> query)
    {
        var results = await query.ToListAsync();
        return results;
    }
}
