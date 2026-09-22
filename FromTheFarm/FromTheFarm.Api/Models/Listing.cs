using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FromTheFarm.Api.Services;

namespace FromTheFarm.Api.Models;

// Mongo collection: Listings
public class Listing : IDocument
{
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("farmerId")]
    public string FarmerId { get; set; } = string.Empty;

    // Used to reconcile an offline-created record once the device reconnects.
    [JsonPropertyName("clientGeneratedId")]
    public string? ClientGeneratedId { get; set; }

    [JsonPropertyName("cropType")]
    public string CropType { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("harvestDate")]
    public DateOnly HarvestDate { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation Location { get; set; } = new();

    [JsonPropertyName("photoUrl")]
    public string? PhotoUrl { get; set; }

    // "Active" | "Matched" | "Expired" | "Deleted"
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Active";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
