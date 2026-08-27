using Infrastructure;

namespace Meta.Users;

/// <summary>
/// Доставляет статы одного игрока за матч в его <see cref="IUserStats"/>.
/// Матч регистрирует по одному такому эффекту на участника.
/// </summary>
[GenerateSerializer]
public class UserStatsSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid UserId { get; init; }
    [Id(1)] public UserStatsDelta Delta { get; init; } = new();

    public Task Execute(IOrleans orleans)
    {
        var stats = orleans.GetGrain<IUserStats>(UserId);
        return stats.Apply(Delta);
    }
}
