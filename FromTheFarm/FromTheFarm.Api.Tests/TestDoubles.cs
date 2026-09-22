using System.Linq.Expressions;
using System.Security.Claims;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FromTheFarm.Api.Tests;

// In-memory stand-in for MongoRepository<T>. Only the by-id operations the
// controllers use are supported — Collection is a LINQ entry point onto a live
// cluster and has no meaningful in-memory equivalent, so reaching for it in a
// test is a mistake worth failing loudly on.
internal sealed class FakeRepository<T> : IMongoRepository<T> where T : class, IDocument
{
    private readonly Dictionary<string, T> _documents = new(StringComparer.Ordinal);

    public FakeRepository(params T[] seed)
    {
        foreach (var item in seed)
        {
            _documents[item.Id] = item;
        }
    }

    // Lets a test assert that a rejected request wrote nothing at all, rather
    // than only that it returned the right status code.
    public int WriteCount { get; private set; }

    public IReadOnlyDictionary<string, T> Documents => _documents;

    public IMongoCollection<T> Collection =>
        throw new NotSupportedException("Querying Collection requires a live cluster.");

    public Task<T?> GetByIdAsync(string id) =>
        Task.FromResult(_documents.TryGetValue(id, out var found) ? found : null);

    public Task<T> UpsertAsync(T item)
    {
        WriteCount++;
        _documents[item.Id] = item;
        return Task.FromResult(item);
    }

    public Task DeleteAsync(string id)
    {
        _documents.Remove(id);
        return Task.CompletedTask;
    }

    // The driver translates these expressions into a Mongo filter; here the same
    // predicate is simply compiled and run over the in-memory documents.
    public Task<List<T>> FindAsync(Expression<Func<T, bool>> filter) =>
        Task.FromResult(_documents.Values.Where(filter.Compile()).ToList());

    public Task<bool> AnyAsync(Expression<Func<T, bool>> filter) =>
        Task.FromResult(_documents.Values.Any(filter.Compile()));
}

internal static class SignedInAs
{
    // Firebase puts the UID in the "sub" claim, which ASP.NET surfaces as
    // NameIdentifier — the same thing ClaimsPrincipalExtensions reads.
    public static ControllerContext User(string uid, string? name = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, uid) };
        if (name is not null)
        {
            // Firebase ID tokens carry the Google display name in the "name" claim.
            claims.Add(new Claim("name", name));
        }

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }
}
