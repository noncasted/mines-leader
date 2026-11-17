namespace Console.Infrastructure.ClientTelemetry;

// The look of the charts of a session, shared by the charts and the scale above them, in the colours of the theme. Inline: the CSS of
// the console is built and committed by a game, and an SVG takes these from its attributes anyway.
public static class TelemetrySessionChartStyle
{
    public const string LineColor = "var(--chart-1, var(--primary))";
    public const string HiddenColor = "var(--muted-foreground)";
    public const string HiddenDash = "4 4";
    public const string HiddenFill = "fill: var(--muted-foreground); fill-opacity: 0.2";
    public const double MinHiddenWidth = 3;

    public static string TickColor(TelemetryTimelineKind kind)
    {
        return kind == TelemetryTimelineKind.Error ? "var(--destructive)" : "var(--muted-foreground)";
    }

    public static string? TickDash(TelemetryTimelineKind kind)
    {
        return kind == TelemetryTimelineKind.Mark ? "2 3" : null;
    }
}
