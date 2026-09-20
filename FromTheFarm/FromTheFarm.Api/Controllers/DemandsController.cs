using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/demands")]
[Authorize]
public class DemandsController : ControllerBase
{
    private readonly IMongoRepository<DemandRequest> _demands;

    public DemandsController(IMongoRepository<DemandRequest> demands)
    {
        _demands = demands;
    }

    public record CreateDemandRequest(
        string? ClientGeneratedId,
        string CropType,
        decimal QuantityNeeded,
        string Unit,
        DateOnly Deadline,
        GeoLocation Location);

    [HttpGet]
    public async Task<ActionResult<List<DemandRequest>>> GetDemands(
        [FromQuery] bool mine = false,
        [FromQuery] string? cropType = null)
    {
        var uid = User.GetFirebaseUid();
        var query = _demands.Collection.AsQueryable()
            .Where(d => d.Status == "Open");

        query = mine ? query.Where(d => d.BuyerId == uid) : query;
        query = cropType is not null ? query.Where(d => d.CropType == cropType) : query;

        var results = await query.ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult<DemandRequest>> CreateDemand([FromBody] CreateDemandRequest request)
    {
        var invalid = RequestValidation.ForDemand(
            request.CropType,
            request.QuantityNeeded,
            request.Unit,
            request.Location,
            request.Deadline,
            Today);

        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var uid = User.GetFirebaseUid();
        var demand = new DemandRequest
        {
            BuyerId = uid,
            ClientGeneratedId = request.ClientGeneratedId,
            CropType = request.CropType,
            QuantityNeeded = request.QuantityNeeded,
            Unit = request.Unit,
            Deadline = request.Deadline,
            Location = request.Location
        };

        var created = await _demands.UpsertAsync(demand);
        return CreatedAtAction(nameof(GetDemands), new { }, created);
    }

    [HttpPut("{demandId}")]
    public async Task<ActionResult<DemandRequest>> UpdateDemand(string demandId, [FromBody] CreateDemandRequest request)
    {
        var invalid = RequestValidation.ForDemand(
            request.CropType,
            request.QuantityNeeded,
            request.Unit,
            request.Location,
            request.Deadline,
            Today);

        if (invalid is not null)
        {
            return BadRequest(invalid);
        }

        var uid = User.GetFirebaseUid();
        var existing = await _demands.GetByIdAsync(demandId);
        if (existing is null)
        {
            return NotFound();
        }

        // The token identifies the caller; the document records its owner. Without
        // this check any authenticated user who knows a demand id could edit it.
        if (!string.Equals(existing.BuyerId, uid, StringComparison.Ordinal))
        {
            return Forbid();
        }

        existing.CropType = request.CropType;
        existing.QuantityNeeded = request.QuantityNeeded;
        existing.Unit = request.Unit;
        existing.Deadline = request.Deadline;
        existing.Location = request.Location;

        var updated = await _demands.UpsertAsync(existing);
        return Ok(updated);
    }

    [HttpDelete("{demandId}")]
    public async Task<IActionResult> DeleteDemand(string demandId)
    {
        var uid = User.GetFirebaseUid();
        var existing = await _demands.GetByIdAsync(demandId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!string.Equals(existing.BuyerId, uid, StringComparison.Ordinal))
        {
            return Forbid();
        }

        existing.Status = "Expired";
        await _demands.UpsertAsync(existing);
        return NoContent();
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
