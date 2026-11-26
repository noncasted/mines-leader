using Shared;

namespace Game.ClientTelemetry;

// The last samples of a previous session, as its client kept them. Each part out of form is dropped alone and counted once: a name
// with the values of its slot, a sample, an infinite value, the values past the names of a sample. NaN is no value, not an error.
public static class TelemetryPreviousSamplesParser
{
    public static (IReadOnlyList<TelemetryPreviousSample> Accepted, int Rejected) Parse(string[]? names, SharedClientTelemetry.Sample[]? samples)
    {
        var declared = names ?? [];
        var slots = ReadNames(declared, out var rejectedNames);
        var all = samples ?? [];
        var valid = all.Where(sample => sample != null && sample.Time >= 0 && sample.Time <= ClientTelemetryLimits.MaxPreviousMs).ToArray();
        var kept = valid.Skip(Math.Max(0, valid.Length - ClientTelemetryLimits.MaxPreviousSamples)).ToArray();
        var rejected = rejectedNames + (all.Length - kept.Length);
        var accepted = new List<TelemetryPreviousSample>();

        foreach (var sample in kept)
        {
            accepted.Add(ReadSample(sample, slots, declared.Length, out var rejectedValues));
            rejected += rejectedValues;
        }

        return (accepted, rejected);
    }

    // The name of each slot, null for a slot whose name is dropped (out of form, repeated or over the limit).
    private static string?[] ReadNames(string[] names, out int rejected)
    {
        var slots = new string?[Math.Min(names.Length, ClientTelemetryLimits.MaxPreviousSampleNames)];
        var seen = new HashSet<string>();

        for (var slot = 0; slot < slots.Length; slot++)
        {
            var name = names[slot];

            if (name != null && TelemetryNames.IsValidName(name) && seen.Add(name))
                slots[slot] = name;
        }

        rejected = names.Length - seen.Count;
        return slots;
    }

    private static TelemetryPreviousSample ReadSample(SharedClientTelemetry.Sample sample, string?[] slots, int declared, out int rejected)
    {
        var values = sample.Values ?? [];
        var named = new Dictionary<string, double>();
        rejected = (values.Length > declared ? 1 : 0) + values.Take(declared).Count(double.IsInfinity);

        for (var slot = 0; slot < Math.Min(values.Length, slots.Length); slot++)
        {
            if (slots[slot] is { } name && double.IsFinite(values[slot]))
                named[name] = values[slot];
        }

        return new TelemetryPreviousSample(sample.Time, sample.Hidden, named);
    }
}
