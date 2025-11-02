using Common.Network;
using Common.Reactive;

namespace MetaGateway.UserFlow.Connection;

public interface IUserSession
{
    IConnection Connection { get; }
    Guid UserId { get; }
    IReadOnlyLifetime Lifetime { get; }
}

public class UserSession : IUserSession
{
    public required IConnection Connection { get; init; }
    public required Guid UserId { get; init; }

    public IReadOnlyLifetime Lifetime => Connection.Lifetime;
}