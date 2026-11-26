using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;

namespace Game.ClientTelemetry;

// The form of a sample in the state of the log: [seq, time, hidden, receivedAt in ms, [values]]. With the settings of the states
// an object repeats its type and every field name, which is most of the size of a log of a thousand samples.
internal sealed class TelemetryStoredSampleJsonConverter : JsonConverter<TelemetryStoredSample>
{
    public override void WriteJson(JsonWriter writer, TelemetryStoredSample? sample, JsonSerializer serializer)
    {
        var value = sample ?? throw new JsonSerializationException("A null sample");

        writer.WriteStartArray();
        writer.WriteValue(value.Seq);
        writer.WriteValue(value.Time);
        writer.WriteValue(value.Hidden);
        writer.WriteValue(new DateTimeOffset(value.ReceivedAt).ToUnixTimeMilliseconds());
        writer.WriteStartArray();

        foreach (var item in value.Values)
            writer.WriteValue(item);

        writer.WriteEndArray();
        writer.WriteEndArray();
    }

    public override TelemetryStoredSample ReadJson(
        JsonReader reader,
        Type objectType,
        TelemetryStoredSample? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        Expect(reader, reader.TokenType, JsonToken.StartArray);

        var seq = Number(reader);
        var time = Number(reader);
        var hidden = reader.ReadAsBoolean() ?? throw new JsonSerializationException("A sample without hidden");
        var receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(Number(reader)).UtcDateTime;

        reader.Read();
        Expect(reader, reader.TokenType, JsonToken.StartArray);

        var values = new List<double?>();

        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            values.Add(Value(reader));

        reader.Read();
        Expect(reader, reader.TokenType, JsonToken.EndArray);

        return new TelemetryStoredSample(seq, time, values, hidden, receivedAt);
    }

    private static long Number(JsonReader reader)
    {
        reader.Read();
        return Convert.ToInt64(reader.Value);
    }

    // jsonb keeps a number in plain digits, so 1e300 comes back as an integer too big for a long. A cast of a BigInteger cuts the
    // digits that do not fit, a parse rounds and gives back the same double.
    private static double? Value(JsonReader reader)
    {
        return reader.Value switch
        {
            null => null,
            double value => value,
            long value => value,
            BigInteger value => double.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            _ => throw new JsonSerializationException($"A sample value of {reader.Value.GetType()}")
        };
    }

    private static void Expect(JsonReader reader, JsonToken token, JsonToken expected)
    {
        if (token != expected)
            throw new JsonSerializationException($"A sample has {token} at {reader.Path}, not {expected}");
    }
}
