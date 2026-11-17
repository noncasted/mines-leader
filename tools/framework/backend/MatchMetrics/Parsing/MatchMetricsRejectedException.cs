namespace Game.MatchMetrics;

// A batch that breaks the contract: answered Rejected, the client stops the stream.
internal sealed class MatchMetricsRejectedException : Exception
{
    public MatchMetricsRejectedException(string message) : base(message)
    {
    }
}
