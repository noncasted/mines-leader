namespace Game.Session;

// The stream of the server of a match in the game metrics (SessionMetrics), driven by the game flow of the session.
public interface ISessionMetrics
{
    // At the start of the game: the stream opens on the lifetime of the session.
    void Start();

    // At the end of the round, winner Guid.Empty - none. Nothing is written when the session has ended already: the
    // round ended with it.
    void End(Guid winner);
}
