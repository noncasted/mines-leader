namespace Game.ClientTelemetry;

// A stored sample for a reader: the values by the names of the metrics. A metric without a value in this sample is absent.
public sealed record TelemetryNamedSample(long Seq, long Time, bool Hidden, DateTime ReceivedAt, IReadOnlyDictionary<string, double> Values)
{
    public static TelemetryNamedSample From(TelemetryStoredSample sample, IReadOnlyList<TelemetryMetricName> names)
    {
        var values = new Dictionary<string, double>();

        foreach (var name in names)
        {
            if (name.Slot < sample.Values.Count && sample.Values[name.Slot] is { } value)
                values[name.Name] = value;
        }

        return new TelemetryNamedSample(sample.Seq, sample.Time, sample.Hidden, sample.ReceivedAt, values);
    }
}
