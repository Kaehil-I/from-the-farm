using System.Text.Json.Serialization;

namespace FromTheFarm.Api.Models;

// Cosmos container: Demands. Partition key: /buyerId
public class DemandRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("buyerId")]
    public string BuyerId { get; set; } = string.Empty;

    [JsonPropertyName("clientGeneratedId")]
    public string? ClientGeneratedId { get; set; }

    [JsonPropertyName("cropType")]
    public string CropType { get; set; } = string.Empty;

    [JsonPropertyName("quantityNeeded")]
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
