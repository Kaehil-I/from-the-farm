using MongoDB.Driver;

namespace FromTheFarm.Api.Services;

// A seam over MongoRepository<T> so controllers can be exercised against an
// in-memory double rather than a live cluster. MongoRepository<T> is the only
// production implementation; this exists for testability, not to support a
// second storage backend.
public interface IMongoRepository<T> where T : class, IDocument
{
    IMongoCollection<T> Collection { get; }

    Task<T?> GetByIdAsync(string id);

    Task<T> UpsertAsync(T item);

    Task DeleteAsync(string id);
}
