namespace Shared
{
    /// <summary>
    /// Read-only доступ к статам игрока. Реализуется и состоянием грейна на сервере,
    /// и проекцией на клиенте, чтобы условия ачивок считались одним и тем же кодом.
    /// </summary>
    public interface IUserStatsState
    {
        long Get(UserStatType type);

        long GetCardsPlayed(CardGroup group);
    }
}
