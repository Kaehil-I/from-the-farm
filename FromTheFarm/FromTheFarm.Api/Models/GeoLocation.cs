using System.Text.Json.Serialization;

namespace FromTheFarm.Api.Models;

public class GeoLocation
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}
