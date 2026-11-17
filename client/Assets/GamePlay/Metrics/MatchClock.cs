using Global.MatchMetrics;
using UnityEngine;

namespace GamePlay.Metrics
{
    // The clock of the match, one for both players: the server stamps every move snapshot with the milliseconds since
    // the start of the game of the session (SharedMoveSnapshot.MatchMs); between the snapshots the clock goes on with
    // the real time of this client from the last stamp. It never goes back, stands at 0 before the first stamp (MatchMs
    // is 0 until the server starts the game) and stops at the snapshot that completes the match. A local pause does not
    // stop it: the server does not stop the match either.
    public class MatchClock : IMatchMetricsClock
    {
        private double _stampSeconds;
        private double _stampReceivedAt;
        private bool _hasStamp;
        private bool _isFrozen;
        private float _last;

        public float Seconds => SecondsAt(Time.realtimeSinceStartupAsDouble);

        public void Receive(long matchMs, double now)
        {
            if (_isFrozen == true || matchMs <= 0)
                return;

            _stampSeconds = matchMs / 1000d;
            _stampReceivedAt = now;
            _hasStamp = true;
        }

        // The snapshot of the completion was received at now: the clock takes its stamp and stands from then on.
        public void Freeze(double now)
        {
            if (_isFrozen == true)
                return;

            SecondsAt(now);
            _isFrozen = true;
        }

        public float SecondsAt(double now)
        {
            if (_isFrozen == true || _hasStamp == false)
                return _last;

            var seconds = (float)(_stampSeconds + (now - _stampReceivedAt));

            if (seconds > _last)
                _last = seconds;

            return _last;
        }
    }
}
