namespace Console.Infrastructure.ClientTelemetry;

// The colors of the bars by depth, from the theme every game has. Inline: see TelemetryLayoutStyle.
public static class TelemetryStartupStyle
{
    public const int AxisParts = 6;
    public const string GridLine = "stroke: var(--border); stroke-width: 1";

    private const int _chartColors = 5;

    public static string Bar(int depth, bool unfinished)
    {
        var dash = unfinished ? "; opacity: 0.5" : string.Empty;

        return $"fill: var(--chart-{depth % _chartColors + 1}, var(--primary)){dash}";
    }
}
