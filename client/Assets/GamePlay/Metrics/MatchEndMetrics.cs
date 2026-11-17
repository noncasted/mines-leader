using System;
using GamePlay.Loop;
using Global.MatchMetrics;
using Internal;

namespace GamePlay.Metrics
{
    // The result of the match for the local player: the event match.end {result = win / lose / leave, seconds - the clock
    // of the match at the end}. The game has no draw.
    public class MatchEndMetrics : IScopeSetup
    {
        public MatchEndMetrics(IMatchMetricsSession session, IGameState state, MatchClock clock)
        {
            _session = session;
            _state = state;
            _clock = clock;
        }

        private readonly IMatchMetricsSession _session;
        private readonly IGameState _state;
        private readonly MatchClock _clock;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _state.CompletedData.ViewNotNull(lifetime, OnCompleted);
        }

        private void OnCompleted(MatchCompletedData data)
        {
            _session.Stream.Event("match.end",
                MatchEventField.Of("result", ToResult(data.Type)),
                MatchEventField.Of("seconds", _clock.Seconds));
        }

        private static string ToResult(MatchResultType type)
        {
            return type switch
            {
                MatchResultType.Win => "win",
                MatchResultType.Lose => "lose",
                MatchResultType.Leave => "leave",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
