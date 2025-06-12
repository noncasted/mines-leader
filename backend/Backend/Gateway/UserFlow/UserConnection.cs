using Common;

namespace Backend.Gateway;

public class UserConnection : IUserConnection
{
    public required Guid UserId { get; init; }
    public required string ConnectionId { get; init; }
    public required ILifetime InternalLifetime { get; init; }
    
    public IReadOnlyLifetime Lifetime => InternalLifetime;
}