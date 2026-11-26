using Infrastructure.State;
using Orleans.Runtime;

namespace Game.ClientTelemetry;

// Holds nothing between the calls: the states are the truth and the reading goes to them without a grain.
public class ClientTelemetryStartup : Grain, IClientTelemetryStartup
{
    public ClientTelemetryStartup(
        [State] State<TelemetryStartupState> startup,
        IStateStorage storage,
        ITelemetryStartupCollection collection,
        ClientTelemetryStorageOptions options,
        TelemetrySessionClock time)
    {
        _startup = startup;
        _storage = storage;
        _collection = collection;
        _options = options;
        _time = time;
    }

    private readonly State<TelemetryStartupState> _startup;
    private readonly IStateStorage _storage;
    private readonly ITelemetryStartupCollection _collection;
    private readonly ClientTelemetryStorageOptions _options;
    private readonly TelemetrySessionClock _time;

    private Guid Id => this.GetPrimaryKey();

    public async Task Store(TelemetryStartupTrace trace)
    {
        DeactivateOnIdle();

        await _startup.Read();

        if (_startup.Value.Final)
            return;

        _startup.Value.Store(Id, trace, _time.UtcNow);

        await _storage.Write(new StateWriteRequest
        {
            Records = new Dictionary<StateIdentity, IStateValue>
            {
                [_storage.IdentityOf<TelemetryStartupState>(Id)] = _startup.Value,
                [_storage.IdentityOf<TelemetryStartupTraceState>(Id)] = TelemetryStartupTraceState.Of(trace)
            }
        });

        await Notify();
    }

    public async Task Sweep()
    {
        DeactivateOnIdle();

        await _startup.Read();

        // No row or a fresh one: a host that missed the push of a delete or a write keeps its entry until it hears again, and
        // its sweep is what comes here for it.
        if (!_startup.Value.IsStored || _startup.Value.UpdatedAt > _time.UtcNow - TimeSpan.FromDays(_options.RetentionDays))
        {
            await Notify();
            return;
        }

        await _storage.Delete(new StateDeleteRequest { Identities = [_storage.IdentityOf<TelemetryStartupState>(Id), _storage.IdentityOf<TelemetryStartupTraceState>(Id)] });

        // A notification only names the key: every host re-reads the row, finds none and drops the entry.
        await Notify();
    }

    // A copy: the collection of this host keeps what it is given.
    private Task Notify()
    {
        return _collection.OnUpdated(Id, _startup.Value.Snapshot());
    }
}
