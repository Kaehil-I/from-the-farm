using System.Text.RegularExpressions;
using FromTheFarm.Api.Models;

namespace FromTheFarm.Api.Services;

// Request validation for the listing, demand and profile endpoints.
//
// The Android client applies the same limits in FormValidation.kt. They are
// repeated here on purpose: the client is one caller among potentially many,
// and a rule that only exists in the app is not a rule the API enforces.
// Thresholds are kept identical to the client's so a payload the app accepts
// is never rejected by the server for a different reason.
public static class RequestValidation
{
    public const int MaxCropTypeLength = 100;
    public const int MaxUnitLength = 20;
    public const decimal MaxQuantity = 1_000_000_000m;

    // Base64 inflates by roughly a third and a Mongo document is capped at
    // 16MB, so a 3MB image leaves ample room for the rest of the document.
    public const int MaxPhotoBytes = 3 * 1024 * 1024;

    public static readonly IReadOnlyList<string> AllowedRoles = new[] { "Farmer", "Buyer" };
    public static readonly IReadOnlyList<string> AllowedLanguages = new[] { "en", "zu", "af" };

    private static readonly Regex PhoneFormat = new(@"^[+0-9 ()-]{7,25}$", RegexOptions.Compiled);

    public static string? CropType(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "cropType is required."
            : value.Length > MaxCropTypeLength
                ? $"cropType must be {MaxCropTypeLength} characters or fewer."
                : null;

    public static string? Quantity(decimal value, string field) =>
        value <= 0
            ? $"{field} must be greater than 0."
            : value > MaxQuantity
                ? $"{field} must not exceed {MaxQuantity:N0}."
                : null;

    public static string? Unit(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unit is required."
            : value.Length > MaxUnitLength
                ? $"unit must be {MaxUnitLength} characters or fewer."
                : null;

    public static string? Location(GeoLocation? value)
    {
        if (value is null)
        {
            return "location is required.";
        }

        if (!double.IsFinite(value.Latitude) || value.Latitude is < -90 or > 90)
        {
            return "latitude must be between -90 and 90.";
        }

        if (!double.IsFinite(value.Longitude) || value.Longitude is < -180 or > 180)
        {
            return "longitude must be between -180 and 180.";
        }

        return null;
    }

    public static string? Deadline(DateOnly value, DateOnly today) =>
        value < today ? "deadline cannot be in the past." : null;

    public static string? Role(string? value) =>
        value is not null && AllowedRoles.Contains(value)
            ? null
            : "role must be either 'Farmer' or 'Buyer'.";

    public static string? Language(string? value) =>
        value is not null && AllowedLanguages.Contains(value)
            ? null
            : "language must be one of 'en', 'zu' or 'af'.";

    public static string? SearchRadiusKm(int value) =>
        value is < 1 or > 100 ? "searchRadiusKm must be between 1 and 100." : null;

    public static string? Phone(string? value) =>
        string.IsNullOrWhiteSpace(value) || PhoneFormat.IsMatch(value.Trim())
            ? null
            : "phone must be 7 to 25 characters long and contain only digits, spaces and the characters + ( ) -.";

    // Returns an error message, or null when the payload is acceptable. On
    // success dataUri holds the value to store, which is null when no photo was
    // supplied — callers treat that as "leave the existing photo alone".
    public static string? Photo(string? base64, out string? dataUri)
    {
        dataUri = null;

        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        var payload = base64.Trim();

        // Tolerate a full data: URI as well as a bare base64 payload.
        var marker = payload.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
        {
            payload = payload[(marker + "base64,".Length)..];
        }

        // Check the encoded length before decoding so an oversized payload is
        // never allocated.
        if ((long)payload.Length * 3 / 4 > MaxPhotoBytes)
        {
            return $"photo must be {MaxPhotoBytes / (1024 * 1024)}MB or smaller.";
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return "photo must be valid base64.";
        }

        if (bytes.Length > MaxPhotoBytes)
        {
            return $"photo must be {MaxPhotoBytes / (1024 * 1024)}MB or smaller.";
        }

        var mediaType = MediaTypeFor(bytes);
        if (mediaType is null)
        {
            return "photo must be a JPEG or PNG image.";
        }

        dataUri = $"data:{mediaType};base64,{payload}";
        return null;
    }

    public static string? ForListing(string? cropType, decimal quantity, string? unit, GeoLocation? location) =>
        CropType(cropType)
        ?? Quantity(quantity, "quantity")
        ?? Unit(unit)
        ?? Location(location);

    public static string? ForDemand(
        string? cropType,
        decimal quantityNeeded,
        string? unit,
        GeoLocation? location,
        DateOnly deadline,
        DateOnly today) =>
        CropType(cropType)
        ?? Quantity(quantityNeeded, "quantityNeeded")
        ?? Unit(unit)
        ?? Location(location)
        ?? Deadline(deadline, today);

    private static string? MediaTypeFor(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        return null;
    }
}
