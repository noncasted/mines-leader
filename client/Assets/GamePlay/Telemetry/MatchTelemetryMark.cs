using Global.Telemetry;
using Internal;

namespace GamePlay.Loop
{
    // The session of a match opens before its scope is loaded (GameLoop): this mark splits the loading and the wait for the
    // players from the play on its time line.
    public class MatchTelemetryMark : IGameStarted
    {
        public MatchTelemetryMark(IClientTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetry _telemetry;

        public void OnGameStarted(IReadOnlyLifetime lifetime)
        {
            _telemetry.Mark("match.started");
        }
    }
}
