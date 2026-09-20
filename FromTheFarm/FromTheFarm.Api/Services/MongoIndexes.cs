using FromTheFarm.Api.Models;
using MongoDB.Driver;

namespace FromTheFarm.Api.Services;

// Index creation is idempotent — asking for an index that already exists with
// the same specification is a no-op — so this runs on every start-up rather
// than being a one-off migration step somebody has to remember.
public static class MongoIndexes
{
    public static async Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var listings = database.GetCollection<Listing>("Listings");
        await listings.Indexes.CreateManyAsync(
            new[]
            {
                // GET /listings?mine=true filters on the owner.
                new CreateIndexModel<Listing>(Builders<Listing>.IndexKeys.Ascending(l => l.FarmerId)),

                // Every browse filters on Status first, then optionally CropType.
                new CreateIndexModel<Listing>(Builders<Listing>.IndexKeys
                    .Ascending(l => l.Status)
                    .Ascending(l => l.CropType)),

                // Offline reconciliation looks a record up by the id the device
                // generated. Sparse because only offline-created records have one.
                new CreateIndexModel<Listing>(
                    Builders<Listing>.IndexKeys.Ascending(l => l.ClientGeneratedId),
                    new CreateIndexOptions { Sparse = true })
            },
            cancellationToken);

        var demands = database.GetCollection<DemandRequest>("Demands");
        await demands.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<DemandRequest>(Builders<DemandRequest>.IndexKeys.Ascending(d => d.BuyerId)),
                new CreateIndexModel<DemandRequest>(Builders<DemandRequest>.IndexKeys
                    .Ascending(d => d.Status)
                    .Ascending(d => d.CropType)),
                new CreateIndexModel<DemandRequest>(
                    Builders<DemandRequest>.IndexKeys.Ascending(d => d.ClientGeneratedId),
                    new CreateIndexOptions { Sparse = true })
            },
            cancellationToken);

        var matches = database.GetCollection<MatchDocument>("Matches");
        await matches.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<MatchDocument>(Builders<MatchDocument>.IndexKeys.Ascending(m => m.FarmerId)),
                new CreateIndexModel<MatchDocument>(Builders<MatchDocument>.IndexKeys.Ascending(m => m.BuyerId)),
                new CreateIndexModel<MatchDocument>(Builders<MatchDocument>.IndexKeys.Ascending(m => m.Status))
            },
            cancellationToken);

        // Enforces one rating per user per match in the database itself. The
        // duplicate check in MatchesController races under concurrent requests;
        // a unique index does not.
        var ratings = database.GetCollection<Rating>("Ratings");
        await ratings.Indexes.CreateOneAsync(
            new CreateIndexModel<Rating>(
                Builders<Rating>.IndexKeys
                    .Ascending(r => r.MatchId)
                    .Ascending(r => r.RaisedByUserId),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
    }
}
