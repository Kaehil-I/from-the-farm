using FromTheFarm.Api.Models;
using MongoDB.Bson;
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
                // generated, scoped to its owner. Unique, so a retried create can
                // never leave two copies behind: the index enforces at the database
                // what the lookup in CreateListing checks for, which a lookup alone
                // cannot guarantee when two retries race.
                //
                // Partial rather than sparse. A compound sparse index still indexes a
                // document when any one key is present, and farmerId always is, so
                // every listing created online would be indexed on a null id and the
                // second one would collide. Filtering on the field actually being a
                // string leaves those documents out of the index entirely.
                // Reference: https://www.mongodb.com/docs/manual/core/index-partial/
                new CreateIndexModel<Listing>(
                    Builders<Listing>.IndexKeys
                        .Ascending(l => l.FarmerId)
                        .Ascending(l => l.ClientGeneratedId),
                    new CreateIndexOptions<Listing>
                    {
                        Unique = true,
                        PartialFilterExpression =
                            Builders<Listing>.Filter.Type(l => l.ClientGeneratedId, BsonType.String)
                    })
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
                // Same reconciliation guarantee as Listings, keyed on the buyer.
                new CreateIndexModel<DemandRequest>(
                    Builders<DemandRequest>.IndexKeys
                        .Ascending(d => d.BuyerId)
                        .Ascending(d => d.ClientGeneratedId),
                    new CreateIndexOptions<DemandRequest>
                    {
                        Unique = true,
                        PartialFilterExpression =
                            Builders<DemandRequest>.Filter.Type(d => d.ClientGeneratedId, BsonType.String)
                    })
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
        // a unique index does not. A second insert for the same pair fails at the
        // driver with a duplicate-key write error rather than silently succeeding.
        // Reference: https://www.mongodb.com/docs/manual/core/index-unique/create-compound/
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
