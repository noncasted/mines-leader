using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace Game.ClientTelemetry;

// _buffer is the truth of an activation: while it is set the session is live, whatever the state says after a failed close.
public class ClientTelemetrySession : Grain, IClientTelemetrySession
{
    public ClientTelemetrySession(
        [State] State<TelemetrySessionState> session,
        IStateStorage storage,
        ITelemetrySessionCollection collection,
        ClientTelemetryStorageOptions options,
        TelemetrySessionClock time,
        ILogger<ClientTelemetrySession> logger)
    {
        _session = session;
        _storage = storage;
        _collection = collection;
        _options = options;
        _time = time;
        _logger = logger;
    }

    private readonly State<TelemetrySessionState> _session;
    private readonly IStateStorage _storage;
    private readonly ITelemetrySessionCollection _collection;
    private readonly ClientTelemetryStorageOptions _options;
    private readonly TelemetrySessionClock _time;
    private readonly ILogger<ClientTelemetrySession> _logger;

    private TelemetrySessionBuffer? _buffer;
    private IGrainTimer? _timer;
    private DateTime _lastBatchAt;

    // The status of a close asked for and not stored yet: the timer and the deactivation repeat this one, not a loss.
    private TelemetrySessionStatus? _closing;

    private Guid Id => this.GetPrimaryKey();
    private DateTime Now => _time.UtcNow;

    // An activation that finds an open row: the one before it is gone without a close, and its memory with it.
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await _session.Read();

        if (!_session.Value.IsStarted || _session.Value.Closed)
            return;

        _session.Value.CloseWithoutData(Now);
        await _session.Write();
        await Notify();
    }

    // The reserve of the timer: a stop of the silo, a migration. After the close by the timer there is nothing left to do here.
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        var wasLive = _buffer != null;

        if (wasLive)
            await CloseLive(_closing ?? TelemetrySessionStatus.Lost);

        _logger.LogInformation("[ClientTelemetry] Session {Session} deactivated: {Reason}, closed here: {WasLive}", Id, reason.ReasonCode, wasLive);
    }

    public async Task Start(TelemetryDevice device, TelemetrySessionTags tags, Guid? startupId, TelemetryPreviousSession? previous)
    {
        await _session.Read();

        if (_session.Value.IsStarted)
            throw new InvalidOperationException($"The session {Id} is already started");

        _session.Value.Open(Id, device, tags.Accepted, startupId, previous, Now);
        await _session.Write();

        _buffer = TelemetrySessionBuffer.Empty(_options, tags.Rejected);
        _lastBatchAt = Now;

        _timer = this.RegisterGrainTimer(
            CloseWhenLost,
            new GrainTimerCreationOptions(_options.LostCheckInterval, _options.LostCheckInterval));

        await Notify();
    }

    public Task<bool> Accept(TelemetryBatch batch)
    {
        if (_buffer is not { } buffer)
        {
            DeactivateOnIdle();
            return Task.FromResult(false);
        }

        buffer.Apply(batch, Now);
        _lastBatchAt = Now;

        return Task.FromResult(true);
    }

    public async Task Close(TelemetrySessionStatus status)
    {
        if (_buffer == null)
        {
            DeactivateOnIdle();
            return;
        }

        _closing = status;
        await CloseLive(status);
    }

    public async Task<TelemetrySessionInfo?> GetInfo()
    {
        await _session.Read();

        var session = _session.Value;

        if (_buffer is not { } buffer)
        {
            DeactivateOnIdle();
            return session.IsStarted ? session.ToInfo() : null;
        }

        return session.ToInfo() with
        {
            Status = TelemetrySessionStatus.Live,
            ClosedAt = null,
            Digest = null,
            LastSeq = buffer.LastSeq,
            LastSampleAt = buffer.LastSampleAt,
            Names = buffer.Names,
            NameSlots = buffer.NameSlots
        };
    }

    public Task<IReadOnlyList<TelemetryStoredSample>?> GetSamples(long afterSeq, int limit)
    {
        return Page(buffer => buffer.Samples.Items, afterSeq, limit, sample => sample.Seq);
    }

    public Task<IReadOnlyList<TelemetryStoredError>?> GetErrors(long afterSeq, int limit)
    {
        return Page(buffer => buffer.Errors.Items, afterSeq, limit, error => error.Seq);
    }

    public Task<IReadOnlyList<TelemetryStoredMark>?> GetMarks(long afterSeq, int limit)
    {
        return Page(buffer => buffer.Marks.Items, afterSeq, limit, mark => mark.Seq);
    }

    public Task<IReadOnlyList<TelemetryStoredSample>?> GetSampleWindow(long from, long to)
    {
        if (_buffer is not { } buffer)
        {
            DeactivateOnIdle();
            return Task.FromResult<IReadOnlyList<TelemetryStoredSample>?>(null);
        }

        return Task.FromResult<IReadOnlyList<TelemetryStoredSample>?>(TelemetryRows.Window(buffer.Samples.Items, from, to));
    }

    public async Task Sweep()
    {
        if (_buffer != null)
            return;

        DeactivateOnIdle();

        await _session.Read();

        var session = _session.Value;

        // The notifications of the branches without a change: a host that missed the push of a close or a delete keeps its entry
        // until it hears again, and its sweep is what comes here for it.
        if (!session.IsStarted)
        {
            await Notify();
            return;
        }

        // Open without the memory: a start stored its row and failed after it. Closed like a row a crash left, never deleted.
        if (!session.Closed)
        {
            session.CloseWithoutData(Now);
            await _session.Write();
            await Notify();
            return;
        }

        if (session.ClosedAt > Now - TimeSpan.FromDays(_options.RetentionDays))
        {
            await Notify();
            return;
        }

        await _storage.Delete(new StateDeleteRequest { Identities = [_storage.IdentityOf<TelemetrySessionState>(Id), _storage.IdentityOf<TelemetrySessionLogState>(Id)] });

        // A notification only names the key: every host re-reads the row, finds none and drops the entry.
        await Notify();
    }

    // A failed close throws to the runtime, which logs it; the next tick tries again, with the status asked for if there was one.
    private async Task CloseWhenLost(CancellationToken cancellation)
    {
        if (_buffer == null)
            return;

        if (_closing is { } status)
            await CloseLive(status);
        else if (Now - _lastBatchAt >= _options.LostAfter)
            await CloseLive(TelemetrySessionStatus.Lost);
    }

    // One batch: a closed row always has its log.
    private async Task CloseLive(TelemetrySessionStatus status)
    {
        var buffer = _buffer ?? throw new InvalidOperationException($"The session {Id} is not live");
        var session = _session.Value;

        session.Close(status, Now, buffer);

        await _storage.Write(new StateWriteRequest
        {
            Records = new Dictionary<StateIdentity, IStateValue>
            {
                [_storage.IdentityOf<TelemetrySessionState>(Id)] = session,
                [_storage.IdentityOf<TelemetrySessionLogState>(Id)] = TelemetrySessionLogState.Of(buffer)
            }
        });

        _buffer = null;
        _timer?.Dispose();
        _timer = null;
        DeactivateOnIdle();

        await Notify();
    }

    // A copy: the collection of this host keeps what it is given, and the state goes on changing here, also by a close not stored yet.
    private Task Notify()
    {
        return _collection.OnUpdated(Id, _session.Value.Snapshot());
    }

    private Task<IReadOnlyList<T>?> Page<T>(Func<TelemetrySessionBuffer, IReadOnlyList<T>> rows, long afterSeq, int limit, Func<T, long> seq)
    {
        if (_buffer is not { } buffer)
        {
            DeactivateOnIdle();
            return Task.FromResult<IReadOnlyList<T>?>(null);
        }

        return Task.FromResult<IReadOnlyList<T>?>(TelemetryRows.Page(rows(buffer), afterSeq, limit, seq));
    }
}
