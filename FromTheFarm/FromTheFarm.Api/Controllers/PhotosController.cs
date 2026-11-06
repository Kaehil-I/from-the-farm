using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FromTheFarm.Api.Controllers;

// Serves listing photos from blob storage. Deliberately anonymous: the app loads
// images over a plain HTTPS request, and the id is a random GUID, so a photo can
// only be fetched by someone who was given its link by the listings API.
[ApiController]
[Route("api/v1/photos")]
[AllowAnonymous]
public class PhotosController : ControllerBase
{
    private readonly IPhotoStore _photos;

    public PhotosController(IPhotoStore photos)
    {
        _photos = photos;
    }

    [HttpGet("{photoId}")]
    public async Task<IActionResult> GetPhoto(string photoId)
    {
        // Reject anything that is not the shape of one of our ids before it
        // reaches storage.
        if (PhotoLinks.IdFrom(PhotoLinks.Route + photoId) is null)
        {
            return NotFound();
        }

        var photo = await _photos.OpenAsync(photoId);
        if (photo is null)
        {
            return NotFound();
        }

        // The content under an id never changes, so it can be cached for good.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.Bytes, photo.ContentType);
    }
}
