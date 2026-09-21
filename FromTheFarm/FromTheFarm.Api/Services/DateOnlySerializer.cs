using System.Globalization;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace FromTheFarm.Api.Services;

// harvestDate, deadline and relevantDate are all DateOnly, which has no
// dependable built-in BSON mapping across driver versions. Storing them as
// "yyyy-MM-dd" strings keeps them readable in Atlas, matches the Date type
// documented in Section 7, and matches the JSON the Android client already
// parses, so nothing downstream changes.
// Reference: https://www.mongodb.com/docs/drivers/csharp/current/serialization/
public class DateOnlySerializer : SerializerBase<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    // Registered globally in Program.cs, so every DateOnly property on every
    // model is stored and read back through this one pair of methods.
    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var value = context.Reader.ReadString();
        return DateOnly.ParseExact(value, Format, CultureInfo.InvariantCulture);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value)
    {
        context.Writer.WriteString(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
