using Internal;

namespace Game.Session;

public class SessionContainerData
{
    public required int ExpectedUsers { get; init; }
    public required Guid Id { get; init; }
    public required ILifetime Lifetime { get; init; }
}
