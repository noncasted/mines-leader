using System;
using Global.MatchMetrics;
using Internal;

namespace GamePlay.Metrics
{
    // The stream of the match by the id of its session (the id the match record of the backend keeps). The stream lives
    // on its own lifetime: the end of the scope first calls Ending, then ends the stream.
    public class MatchMetricsSession : IMatchMetricsSession, IScopeSetup
    {
        public MatchMetricsSession(IMatchMetrics metrics, MatchClock clock, Guid matchId)
        {
            _metrics = metrics;
            _clock = clock;
            _matchId = matchId;
        }

        private readonly IMatchMetrics _metrics;
        private readonly MatchClock _clock;
        private readonly Guid _matchId;
        private readonly EventSource _ending = new();

        private IMatchMetricsStream _stream;

        public IMatchMetricsStream Stream =>
            _stream ?? throw new InvalidOperationException("[Metrics] The stream before the setup of the match");

        public IEventSource Ending => _ending;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var streamLifetime = new Lifetime();
            _stream = _metrics.Open(streamLifetime, _matchId, _clock);
            lifetime.Listen(() => End(streamLifetime));
        }

        private void End(Lifetime streamLifetime)
        {
            try
            {
                _ending.Invoke();
            }
            finally
            {
                streamLifetime.Terminate();
            }
        }
    }
}
