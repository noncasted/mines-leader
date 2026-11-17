namespace Game.GamePlay;

// The clock of the match, one for both players: the server time since the start of the game of the session. Stamped on
// every snapshot (SharedMoveSnapshot.MatchMs), the clients run their clock of the match metrics from it. One game per
// session: a rematch is a new session with its own clock.
public interface IMatchClock
{
    // 0 before the start.
    long Ms { get; }
    double Seconds { get; }

    void Start();
}
