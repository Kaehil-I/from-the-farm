using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace FromTheFarm.Api.Services;

// Blob storage on MongoDB GridFS: the file is split into chunks held in the
// photos.chunks collection, with its metadata in photos.files. Using the cluster
// we already run avoids a second storage account, key and bill.
// Reference: https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/gridfs/
//
// The id is a random GUID rather than Mongo's default ObjectId. ObjectIds are
// partly sequential and so guessable, and the photo endpoint is public because
// the app loads images without an auth header; an unguessable id is what keeps
// one farm's photo from being enumerated.
public sealed class GridFsPhotoStore : IPhotoStore
{
    private readonly GridFSBucket<string> _bucket;

    public GridFsPhotoStore(IMongoDatabase database)
    {
        _bucket = new GridFSBucket<string>(database, new GridFSBucketOptions { BucketName = "photos" });
    }

    public async Task<string> SaveAsync(byte[] bytes, string contentType)
    {
        var id = Guid.NewGuid().ToString("N");
        await _bucket.UploadFromBytesAsync(id, id, bytes, new GridFSUploadOptions
        {
            Metadata = new BsonDocument("contentType", contentType)
        });
        return id;
    }

    public async Task<StoredPhoto?> OpenAsync(string id)
    {
        var filter = Builders<GridFSFileInfo<string>>.Filter.Eq(f => f.Id, id);
        using var cursor = await _bucket.FindAsync(filter);
        var info = (await cursor.ToListAsync()).FirstOrDefault();
        if (info is null)
        {
            return null;
        }

        var bytes = await _bucket.DownloadAsBytesAsync(id);
        var contentType = info.Metadata is not null && info.Metadata.TryGetValue("contentType", out var value)
            ? value.AsString
            : "image/jpeg";
        return new StoredPhoto(bytes, contentType);
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _bucket.DeleteAsync(id);
        }
        catch (GridFSFileNotFoundException)
        {
            // Already gone: the goal state is reached.
        }
    }
}
