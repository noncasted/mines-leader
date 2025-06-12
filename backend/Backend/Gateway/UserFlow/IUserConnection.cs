using Common;

namespace Backend.Gateway;

public interface IUserConnection
{
    Guid UserId { get; }
    string ConnectionId { get; }
    IReadOnlyLifetime Lifetime { get; }
}