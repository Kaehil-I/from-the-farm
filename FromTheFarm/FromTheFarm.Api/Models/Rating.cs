using System.Text.Json.Serialization;

namespace FromTheFarm.Api.Models;

// Cosmos container: Ratings. Partition key: /matchId
public class Rating
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("matchId")]
    public string MatchId { get; set; } = string.Empty;

    [JsonPropertyName("raisedByUserId")]
    public string RaisedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("thumbsUp")]
    public bool ThumbsUp { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
