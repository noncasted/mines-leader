namespace Game.ClientTelemetry;

// Goes over the compact states in memory (ITelemetrySessionCollection). A condition on the summary leaves out the sessions without
// one: the live ones and the ones lost in a crash.
public sealed record TelemetrySessionFilter
{
    public TelemetrySessionStatus? Status { get; init; }
    public string? Platform { get; init; }
    public string? BuildVersion { get; init; }

    // A session must hold all of them.
    public IReadOnlyList<string> Tags { get; init; } = [];

    // Metric with AverageBelow / AverageAbove: "the sessions with an average fps under 30".
    public string? Metric { get; init; }
    public double? AverageBelow { get; init; }
    public double? AverageAbove { get; init; }

    // Errors with their repeats.
    public int? MinErrors { get; init; }

    // The sessions that refer to one startup trace.
    public Guid? StartupId { get; init; }

    // The sessions whose client reported this one as its previous session that did not close.
    public Guid? PreviousId { get; init; }

    public int Offset { get; init; }
    public int Limit { get; init; } = 100;

    public bool Matches(TelemetrySessionInfo session)
    {
        if (Status != null && session.Status != Status)
            return false;

        if (StartupId != null && session.StartupId != StartupId)
            return false;

        if (PreviousId != null && session.Previous?.SessionId != PreviousId)
            return false;

        if (Platform != null && session.Device.Platform != Platform)
            return false;

        if (BuildVersion != null && session.Device.BuildVersion != BuildVersion)
            return false;

        if (Tags.Except(session.Tags).Any())
            return false;

        if (MinErrors != null && (session.Digest == null || session.Digest.ErrorCount < MinErrors))
            return false;

        return Metric == null || MatchesMetric(session);
    }

    private bool MatchesMetric(TelemetrySessionInfo session)
    {
        var metric = session.Digest?.Summary.Metrics.FirstOrDefault(item => item.Name == Metric);

        return metric != null
               && (AverageBelow == null || metric.Average < AverageBelow)
               && (AverageAbove == null || metric.Average > AverageAbove);
    }
}
