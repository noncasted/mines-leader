using Game.Session;
using Microsoft.Extensions.Logging;

namespace Game.Lobbies;

// A user holds a reserved place in one lobby of the server at a time: a place taken in another one drops the previous.
internal class LobbyReservations
{
    public LobbyReservations(ILogger logger)
    {
        _logger = logger;
    }

    private readonly ILogger _logger;

    // The lobby each user has reserved a place in last; entries of a closed lobby are removed with it.
    private readonly Dictionary<Guid, ISession> _reserved = new();
    private readonly Lock _lock = new();

    public void OnReserved(ISession session, Guid userId)
    {
        ISession? previous;

        lock (_lock)
        {
            _reserved.TryGetValue(userId, out previous);
            _reserved[userId] = session;
        }

        if (previous == null || previous == session)
            return;

        _logger.LogInformation("{UserId} [Lobby] The place in lobby session {SessionID} is dropped: " +
                               "the user took a place in another lobby",
            userId,
            previous.Id);

        previous.CancelReservation(userId);
    }

    public void Forget(ISession session)
    {
        lock (_lock)
        {
            foreach (var (userId, reserved) in _reserved)
            {
                if (reserved == session)
                    _reserved.Remove(userId);
            }
        }
    }
}
