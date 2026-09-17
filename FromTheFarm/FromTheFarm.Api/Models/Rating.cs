using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FromTheFarm.Api.Services;

namespace FromTheFarm.Api.Models;

// Mongo collection: Ratings
public class Rating : IDocument
{
    [BsonId]
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
