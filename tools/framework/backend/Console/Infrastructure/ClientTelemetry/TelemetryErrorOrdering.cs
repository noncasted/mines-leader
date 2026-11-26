using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

public static class TelemetryErrorOrdering
{
    public static IReadOnlyList<TelemetryGlobalErrorGroup> Apply(IEnumerable<TelemetryGlobalErrorGroup> groups, TelemetryErrorOrder order)
    {
        var ordered = order == TelemetryErrorOrder.Frequency
            ? groups.OrderByDescending(group => group.Count).ThenByDescending(group => group.LastSeenAt)
            : groups.OrderByDescending(group => group.LastSeenAt).ThenByDescending(group => group.Count);

        return ordered.ThenBy(group => group.Fingerprint, StringComparer.Ordinal).ToArray();
    }
}
