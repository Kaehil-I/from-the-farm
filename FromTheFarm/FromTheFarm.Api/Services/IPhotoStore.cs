namespace FromTheFarm.Api.Services;

public sealed record StoredPhoto(byte[] Bytes, string ContentType);

// Blob storage for listing photos. Images are binary objects that do not belong
// inside a database document (16MB cap, and every listing read would drag the
// bytes along), so they are stored separately and a listing keeps only a URL.
// An interface so controllers can be tested without a live cluster.
public interface IPhotoStore
{
    // Stores the image and returns its identifier.
    Task<string> SaveAsync(byte[] bytes, string contentType);

    Task<StoredPhoto?> OpenAsync(string id);

    // Removing something that is not there is not an error.
    Task DeleteAsync(string id);
}
