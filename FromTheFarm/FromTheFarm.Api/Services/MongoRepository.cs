using System.Linq.Expressions;
using MongoDB.Driver;

namespace FromTheFarm.Api.Services;

// Thin generic wrapper over a single Mongo collection, kept deliberately simple:
// no unit of work, no specification pattern. Each collection has genuinely
// different access patterns, so the by-id operations and two filtered reads live
// here and anything more bespoke (the browse endpoints' optional filters) still
// builds its query on Collection.
//
// Two things worth knowing:
//   - There is no partition-key concept, so these methods take an id alone.
//   - Mongo creates a collection on first write, so there is no
//     CreateContainerIfNotExists equivalent and no blocking I/O in the
//     constructor.
// Reference: https://www.mongodb.com/docs/drivers/csharp/current/crud/
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

    // One method covers both create and update: ReplaceOne with IsUpsert inserts
    // when no document matches the id and replaces the whole document when one
    // does. Callers therefore read, mutate and save, and never have to know
    // which of the two happened.
    public async Task<T> UpsertAsync(T item)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, item.Id);
        await _collection.ReplaceOneAsync(filter, item, new ReplaceOptions { IsUpsert = true });
        return item;
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.Find(filter).ToListAsync();
    }

    // Limit(1) so the server stops at the first match — this backs the
    // has-this-user-already-rated check, where only existence matters.
    public async Task<bool> AnyAsync(Expression<Func<T, bool>> filter)
    {
        return await _collection.Find(filter).Limit(1).AnyAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        await _collection.DeleteOneAsync(filter);
    }
}
