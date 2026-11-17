namespace Console.Infrastructure.ClientTelemetry;

// What the pages of the telemetry need beyond the utility classes every game already has in its CSS. A game builds that CSS by hand
// and commits it, so a new Tailwind class of the framework stays without a rule until the game rebuilds it; inline styles need no step.
public static class TelemetryLayoutStyle
{
    public const string Stats = "display: grid; grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr)); gap: 1rem";
    public const string Charts = "display: grid; grid-template-columns: repeat(auto-fit, minmax(30rem, 1fr)); gap: 1rem";
    public const string Wrap = "flex-wrap: wrap";
}
