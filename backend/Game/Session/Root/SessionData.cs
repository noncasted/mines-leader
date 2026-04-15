using Common.Reactive;
using Shared;

namespace Game.Session;

public interface ISessionData
{
    int ExpectedUsers { get; }
    SessionType Type { get; }
    Guid Id { get; }
    IReadOnlyLifetime Lifetime { get; }
}

public class SessionData : ISessionData
{
    public required int ExpectedUsers { get; init; }
    public required SessionType Type { get; init; }
    public required Guid Id { get; init; }
    public required IReadOnlyLifetime Lifetime { get; init; }
}