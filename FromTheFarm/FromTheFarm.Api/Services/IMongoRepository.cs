using System.Linq.Expressions;
using MongoDB.Driver;

namespace FromTheFarm.Api.Services;

// A seam over MongoRepository<T> so controllers can be exercised against an
// in-memory double rather than a live cluster. MongoRepository<T> is the only
// production implementation; this exists for testability, not to support a
// second storage backend.
//
// The constraint on T is what lets the implementation build an _id filter for
// any model without reflection: IDocument guarantees a settable Id.
public interface IMongoRepository<T> where T : class, IDocument
{
    // Exposed for the queries that do not fit the methods below, such as the
    // browse endpoints' optional filters. An in-memory double has no meaningful
    // equivalent, so anything reaching for this cannot be unit tested.
    IMongoCollection<T> Collection { get; }

    Task<T?> GetByIdAsync(string id);

    Task<T> UpsertAsync(T item);

    Task DeleteAsync(string id);

    // Filtered reads that the match feed and the one-rating-per-user check need.
    // They take an expression rather than exposing Collection so an in-memory
    // double can evaluate the same predicate the driver translates for Mongo.
    Task<List<T>> FindAsync(Expression<Func<T, bool>> filter);

    Task<bool> AnyAsync(Expression<Func<T, bool>> filter);
}
