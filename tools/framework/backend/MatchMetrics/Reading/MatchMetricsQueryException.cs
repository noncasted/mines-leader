namespace Game.MatchMetrics;

// A query out of the rules: the endpoints answer 400 with the message.
public sealed class MatchMetricsQueryException : Exception
{
    public MatchMetricsQueryException(string message) : base(message)
    {
    }
}
