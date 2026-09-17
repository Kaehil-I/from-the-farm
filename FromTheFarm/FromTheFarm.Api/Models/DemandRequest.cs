using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FromTheFarm.Api.Services;

namespace FromTheFarm.Api.Models;

// Mongo collection: Demands
public class DemandRequest : IDocument
{
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("buyerId")]
    public string BuyerId { get; set; } = string.Empty;

    [JsonPropertyName("clientGeneratedId")]
    public string? ClientGeneratedId { get; set; }

    [JsonPropertyName("cropType")]
    public string CropType { get; set; } = string.Empty;

    [JsonPropertyName("quantityNeeded")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal QuantityNeeded { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("deadline")]
    public DateOnly Deadline { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation Location { get; set; } = new();

    // "Open" | "Matched" | "Expired"
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Open";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
