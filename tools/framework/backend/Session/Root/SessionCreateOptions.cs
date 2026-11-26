using Shared;

namespace Game.Session;

public class SessionCreateOptions
{
    public required SessionType Type { get; init; }

    // The session starts handling its users once this many have joined (bots included); 0 - at once.
    public required int ExpectedUsers { get; init; }

    // Places of the users that hold the session (joined, joining, reserved, away); bots don't take one.
    public required int MaxUsers { get; init; }
    public required bool IsPrivate { get; init; }

    // Set by a creator that has to know the id before the session exists (a lobby that registers its code first).
    public Guid Id { get; init; } = Guid.NewGuid();

    // A reservation lives this long. A session that nobody enters is closed after it, and so is one whose expected
    // users have not all joined in this time (a player that never came must not hold it forever). Shorter than the
    // keep-alive abort of a connected socket that is not read yet.
    public TimeSpan JoinTimeout { get; init; } = TimeSpan.FromSeconds(30);

    // How long the seat of a user whose connection is lost (without SharedSessionPlayer.Leave) waits for its return:
    // its entities stay, the others get RemoteAway. Zero - no reconnect, a lost connection is a leave.
    public TimeSpan ReconnectWindow { get; init; } = TimeSpan.Zero;

    // The clock of the reservations and the windows.
    public TimeProvider Time { get; init; } = TimeProvider.System;

    internal bool IsReconnectable => ReconnectWindow > TimeSpan.Zero;
}
