using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FromTheFarm.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly IMongoDatabase _database;

    public HealthController(IMongoDatabase database)
    {
        _database = database;
    }

    // Deliberately unauthenticated: a container host needs a health check target
    // it can call without credentials, and it gives us a way to prove the
    // database connection works without minting a Firebase token first.
    //
    // The error branch returns only the exception type, never its message — a
    // public endpoint should not leak host names or connection details.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return Ok(new { status = "healthy", database = _database.DatabaseNamespace.DatabaseName });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", error = ex.GetType().Name });
        }
    }
}
