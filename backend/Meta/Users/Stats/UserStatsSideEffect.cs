using Infrastructure;

namespace Meta.Users;

/// <summary>
/// Delivers one player's match stats to their <see cref="IUserStats"/>.
/// The match registers one such effect per participant.
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
