using Global.MatchMetrics;
using Internal;

namespace GamePlay.Metrics
{
    public interface IMatchMetricsSession
    {
        // The stream of the local player in this match, open from the setup of the scope of the match to its end.
        IMatchMetricsStream Stream { get; }

        // Called once at the end of the match scope while the stream is still open: a writer that holds something
        // unwritten writes it here. A flush on the end of a lifetime of its own could come after the end of the stream
        // and throw.
        IEventSource Ending { get; }
    }
}
