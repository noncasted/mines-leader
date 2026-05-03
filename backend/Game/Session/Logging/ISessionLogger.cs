using Shared;

namespace Game.Session;

public interface ISessionLogger
{
    void RegisterPlayers(IReadOnlyList<IUser> users);
    void LogSessionCreated(SessionType type, Guid sessionId);
    void LogGameStarted(IReadOnlyList<Guid> playerIds);
    void LogRoundStart(Guid playerId, int roundNumber);
    void LogRoundEnd(Guid playerId, int roundNumber);
    void LogCellOpened(Guid playerId, Position position, bool hasMine, bool shieldConsumed);
    void LogFlagSet(Guid playerId, Position position);
    void LogFlagRemoved(Guid playerId, Position position);
    void LogCardUsed(Guid playerId, CardType cardType, int manaCost, bool success);
    void LogHealthChanged(Guid playerId, int oldHealth, int newHealth);
    void LogManaChanged(Guid playerId, int current, int max);
    void LogBotTurnStart(Guid botId);
    void LogBotAction(string actionType, string details);
    void LogBotProfile(Guid botId, string profile);
    void LogTurnSkipped(Guid playerId);
    void LogGameOver(Guid winnerId, string reason);
    void Log(string message);
}