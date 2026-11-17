using Infrastructure;
using Infrastructure.State;

namespace Game.ClientTelemetry;

// The stored side of a session for the hosts: no grain is activated here.
internal sealed class TelemetrySessionStore
{
    public TelemetrySessionStore(ITelemetrySessionCollection sessions, IStateStorage storage)
    {
        _sessions = sessions;
        _storage = storage;
    }

    private readonly ITelemetrySessionCollection _sessions;
    private readonly IStateStorage _storage;

    // Null: no such session.
    public Task<TelemetrySessionState?> Find(Guid id)
    {
        return _sessions.Find(_storage, id, session => session.IsStarted);
    }

    public Task<TelemetrySessionLogState> ReadLog(Guid id)
    {
        return _storage.Read<TelemetrySessionLogState>(_storage.IdentityOf<TelemetrySessionLogState>(id));
    }
}
