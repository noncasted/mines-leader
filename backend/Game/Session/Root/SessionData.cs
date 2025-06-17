using Common;

namespace Game;

public interface ISessionData
{
    public int ExpectedUsers { get; }
    public string Type { get; }
    public Guid Id { get; }
    public IReadOnlyLifetime Lifetime { get; }
}

public class SessionData : ISessionData
{
    public required int ExpectedUsers { get; init; }
    public required string Type { get; init; }
    public required Guid Id { get; init; }
    public required IReadOnlyLifetime Lifetime { get; init; }
}