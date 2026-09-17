using System.Text.Json.Serialization;

namespace FromTheFarm.Api.Models;

// Cosmos container: Listings. Partition key: /farmerId
public class Listing
{
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
