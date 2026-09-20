using MongoDB.Driver;

namespace FromTheFarm.Api.Services;

// Thin generic wrapper over a single Mongo collection, kept deliberately simple:
// no unit of work, no specification pattern. Each collection has genuinely
// different access patterns, so controllers query LINQ directly against
// Collection rather than hiding it behind an over-abstracted interface.
//
// Two things worth knowing:
//   - There is no partition-key concept, so these methods take an id alone.
//   - Mongo creates a collection on first write, so there is no
//     CreateContainerIfNotExists equivalent and no blocking I/O in the
//     constructor.
public class MongoRepository<T> : IMongoRepository<T> where T : class, IDocument
{
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<T>(collectionName);
    }

    public IMongoCollection<T> Collection => _collection;

    public async Task<T?> GetByIdAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T> UpsertAsync(T item)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, item.Id);
        await _collection.ReplaceOneAsync(filter, item, new ReplaceOptions { IsUpsert = true });
        return item;
    }

    public async Task DeleteAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        await _collection.DeleteOneAsync(filter);
    }
}
