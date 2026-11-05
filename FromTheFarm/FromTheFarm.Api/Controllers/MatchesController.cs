using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/matches")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMongoRepository<UserProfile> _users;
    private readonly IMongoRepository<MatchDocument> _matches;
    private readonly MatchGenerationService _generation;
    private readonly NotificationService _notifications;

    public MatchesController(
        IMongoRepository<UserProfile> users,
        IMongoRepository<MatchDocument> matches,
        MatchGenerationService generation,
        NotificationService notifications)
    {
        _users = users;
        _matches = matches;
        _generation = generation;
        _notifications = notifications;
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
            await _generation.GenerateForFarmerAsync(uid, profile.SearchRadiusKm);
        }
        else
        {
            await _generation.GenerateForBuyerAsync(uid, profile.SearchRadiusKm);
        }

        var results = await _matches.FindAsync(m => m.FarmerId == uid || m.BuyerId == uid);
        results = await _generation.DropStaleSuggestionsAsync(results);

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

        // Confirming releases both parties' contact details, so it must not
        // succeed against a listing that was deleted or a demand that has expired
        // since the suggestion was made.
        if (!await _generation.IsStillAvailableAsync(match))
        {
            await _matches.DeleteAsync(match.Id);
            return NotFound("This match is no longer available.");
        }

        match.Status = "Confirmed";
        match.ConfirmedAt = DateTime.UtcNow;
        await _matches.UpsertAsync(match);
        await _notifications.NotifyAsync(OtherParty(match, uid), MatchNotification.Confirmed, match);

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
        await _notifications.NotifyAsync(OtherParty(match, uid), MatchNotification.Completed, match);

        return NoContent();
    }

    [HttpPost("{matchId}/rating")]
    public async Task<IActionResult> SubmitRating(string matchId, [FromBody] RatingRequest request, [FromServices] IMongoRepository<Rating> ratings)
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
        var alreadyRated = await ratings.AnyAsync(r => r.MatchId == matchId && r.RaisedByUserId == uid);
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

    private static string OtherParty(MatchDocument match, string uid) =>
        match.FarmerId == uid ? match.BuyerId : match.FarmerId;
}
