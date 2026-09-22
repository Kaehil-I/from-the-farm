using FromTheFarm.Api.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class DateOnlySerializerTests
{
    // Registering a serializer for the same type twice in one process throws,
    // so guard this in case another test class (or Program.cs, in an
    // integration test setup) already registered it first.
    static DateOnlySerializerTests()
    {
        try { BsonSerializer.RegisterSerializer(new DateOnlySerializer()); }
        catch (BsonSerializationException) { /* already registered — fine */ }
    }

    private class Wrapper
    {
        public DateOnly Value { get; set; }
    }

    [Fact]
    public void SerializesAsAnIsoDateString_NotANativeBsonDate()
    {
        // harvestDate/deadline/relevantDate are stored as "yyyy-MM-dd" strings,
        // deliberately not BSON's native date type — see the comment in
        // DateOnlySerializer for why.
        var wrapper = new Wrapper { Value = new DateOnly(2026, 3, 4) };

        var bson = wrapper.ToBsonDocument();

        Assert.Equal(BsonType.String, bson["Value"].BsonType);
        Assert.Equal("2026-03-04", bson["Value"].AsString);
    }

    [Fact]
    public void RoundTrips_SerializeThenDeserialize_ReturnsTheOriginalDate()
    {
        var original = new Wrapper { Value = new DateOnly(2026, 12, 25) };

        var bson = original.ToBsonDocument();
        var roundTripped = BsonSerializer.Deserialize<Wrapper>(bson);

        Assert.Equal(original.Value, roundTripped.Value);
    }

    [Fact]
    public void PadsSingleDigitMonthsAndDaysWithLeadingZeros()
    {
        // A sanity check on the exact format string ("yyyy-MM-dd") — 5 January
        // should never come out as "2026-1-5".
        var wrapper = new Wrapper { Value = new DateOnly(2026, 1, 5) };

        var bson = wrapper.ToBsonDocument();

        Assert.Equal("2026-01-05", bson["Value"].AsString);
    }
}