using Infrastructure.State;

namespace Game.ClientTelemetry;

// The stored side of the startup traces for the hosts: no grain is activated here.
internal sealed class TelemetryStartupStore
{
    public TelemetryStartupStore(ITelemetryStartupCollection startups, IStateStorage storage)
    {
        _startups = startups;
        _storage = storage;
    }

    private readonly ITelemetryStartupCollection _startups;
    private readonly IStateStorage _storage;

    public IReadOnlyList<TelemetryStartupInfo> List(int limit)
    {
        return _startups.Values
                        .Select(startup => startup.ToInfo())
                        .OrderByDescending(startup => startup.ReceivedAt)
                        .ThenBy(startup => startup.Id)
                        .Take(limit)
                        .ToArray();
    }

    // Null: no such record.
    public Task<TelemetryStartupState?> Find(Guid id)
    {
        return _startups.Find(_storage, id, startup => startup.IsStored);
    }

    public async Task<IReadOnlyList<TelemetryStartupSpan>> ReadSpans(Guid id)
    {
        return (await _storage.Read<TelemetryStartupTraceState>(_storage.IdentityOf<TelemetryStartupTraceState>(id))).Spans;
    }
}
