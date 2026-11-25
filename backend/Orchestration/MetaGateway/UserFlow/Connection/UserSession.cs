using System.Diagnostics;
using Common.Network;
using Internal;

namespace MetaGateway.UserFlow.Connection;

public interface IUserSession
{
    IConnection Connection { get; }
    Guid UserId { get; }
    IReadOnlyLifetime Lifetime { get; }

    // The trace of the root span "Meta connect" of the socket, default when the source was not sampled.
    ActivityTraceId TraceId { get; }
}

public class UserSession : IUserSession
{
    public required IConnection Connection { get; init; }
    public required Guid UserId { get; init; }
    public required ActivityTraceId TraceId { get; init; }

    public IReadOnlyLifetime Lifetime => Connection.Lifetime;
}