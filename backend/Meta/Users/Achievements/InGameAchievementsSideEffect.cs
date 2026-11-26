using Infrastructure;

namespace Meta.Users;

/// <summary>
/// Recalculates the player's achievements. Registered by the <see cref="IUserStats"/> grain
/// after the stats are updated.
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