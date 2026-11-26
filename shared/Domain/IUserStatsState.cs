namespace Shared
{
    /// <summary>
    /// Read-only access to the player stats. Implemented by both the grain state on the server
    /// and the projection on the client, so that achievement conditions are evaluated by the same code.
    /// </summary>
    public interface IUserStatsState
    {
        long Get(UserStatType type);

        long GetCardsPlayed(CardGroup group);
    }
}
