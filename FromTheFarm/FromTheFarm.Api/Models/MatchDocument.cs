using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FromTheFarm.Api.Services;

namespace FromTheFarm.Api.Models;

// Embedded snapshot so the feed reads in a single document fetch —
// no join back to the Listings/Demands containers needed for the list view.
public class CounterpartSnapshot
{
    [JsonPropertyName("cropType")]
    public string CropType { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("distanceKm")]
    public double DistanceKm { get; set; }

    [JsonPropertyName("relevantDate")]
    public DateOnly RelevantDate { get; set; }
}

// Mongo collection: Matches
//
// NOTE (student-project simplification): the design doc discusses partitioning
// Matches by the querying user's ID, but a Match has two legitimate "owners"
// (the farmer and the buyer) — a single partition key can't cleanly serve both
// sides without either duplicating the document or fanning out writes. For the
// scope of this POE we self-partition on /id and query with a cross-partition
// filter on farmerId or buyerId. This costs more RUs at scale than a
// purpose-built partition strategy would, but it's correct and simple, which
// matters more at this project's traffic volume than RU efficiency does.
public class MatchDocument : IDocument
{
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("listingId")]
    public string ListingId { get; set; } = string.Empty;

    [JsonPropertyName("demandRequestId")]
    public string DemandRequestId { get; set; } = string.Empty;

    [JsonPropertyName("farmerId")]
    public string FarmerId { get; set; } = string.Empty;

    [JsonPropertyName("buyerId")]
    public string BuyerId { get; set; } = string.Empty;

    // 0.0–1.0, see MatchingService for the weighted formula.
    [JsonPropertyName("score")]
    public double Score { get; set; }

    // "Suggested" | "Confirmed" | "Completed"
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Suggested";

    [JsonPropertyName("counterpartSnapshot")]
    public CounterpartSnapshot CounterpartSnapshot { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("confirmedAt")]
    public DateTime? ConfirmedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
