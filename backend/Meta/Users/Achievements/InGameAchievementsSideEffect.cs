using Infrastructure;

namespace Meta.Users;

/// <summary>
/// Пересчитывает ачивки игрока. Регистрируется грейном <see cref="IUserStats"/>
/// после того, как статы обновились.
/// </summary>
[GenerateSerializer]
public class InGameAchievementsSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid UserId { get; init; }

    public Task Execute(IOrleans orleans)
    {
        var achievements = orleans.GetGrain<IUserInGameAchievements>(UserId);
        return achievements.Evaluate();
    }
}