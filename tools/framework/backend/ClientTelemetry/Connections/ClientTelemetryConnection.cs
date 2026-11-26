using System.Diagnostics;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.ClientTelemetry;

// The telemetry of one socket: at most one open session, the only one its client can reach, since the guid comes from here. The
// calls are one at a time: the handlers of a socket run side by side. Every call is a boundary: telemetry never fails a socket
// of a player, a failure stops the telemetry of this session (enabled false), and the timer of the grain closes what is left.
public sealed class ClientTelemetryConnection
{
    public ClientTelemetryConnection(IClientTelemetrySwitch telemetrySwitch, IOrleans orleans, ILogger<ClientTelemetryConnection> logger)
    {
        _switch = telemetrySwitch;
        _orleans = orleans;
        _logger = logger;
        _startups = new ClientTelemetryStartups(telemetrySwitch, orleans, logger);
    }

    private static readonly SharedClientTelemetry.SessionStarted _notStarted = new() { Enabled = false };
    private static readonly SharedClientTelemetry.BatchResult _stop = new() { Enabled = false };
    private static readonly SharedClientTelemetry.BatchResult _goOn = new() { Enabled = true };

    private readonly IClientTelemetrySwitch _switch;
    private readonly IOrleans _orleans;
    private readonly ILogger<ClientTelemetryConnection> _logger;
    private readonly ClientTelemetryStartups _startups;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Guid? _current;
    private bool _isDisconnected;

    public async Task<SharedClientTelemetry.SessionStarted> Start(SharedClientTelemetry.SessionStart request)
    {
        await _gate.WaitAsync();

        try
        {
            await CloseCurrent(TelemetrySessionStatus.Ended);

            if (_isDisconnected || !_switch.IsEnabled)
                return _notStarted;

            var id = Guid.NewGuid();

            var tags = TelemetryTagsParser.Parse(request.Tags);
            var startup = _startups.Link(request.StartupId);

            var previous = TelemetryPreviousParser.Parse(request.Previous);

            tags = tags with { Rejected = tags.Rejected + (startup.IsRejected ? 1 : 0) + previous.Rejected };

            await _orleans.GetGrain<IClientTelemetrySession>(id).Start(TelemetryDeviceParser.Parse(request.Device), tags, startup.Id, previous.Session);
            _current = id;

            return new SharedClientTelemetry.SessionStarted { Enabled = true, SessionId = id };
        }
        catch (Exception exception)
        {
            _logger.LogWarning("[ClientTelemetry] A session is not started: {Failure}", exception.GetType().Name);
            return _notStarted;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Needs no open session and has its own gate: the write of a trace must not look like a second batch in flight. backendTrace:
    // the trace of the authorization of this socket in the OTLP backend, default when the game has none.
    public Task<SharedClientTelemetry.StartupTraceResult> AcceptStartup(SharedClientTelemetry.StartupTrace trace, ActivityTraceId backendTrace)
    {
        return _startups.Accept(trace, backendTrace);
    }

    // A client keeps one batch in flight, so a second one breaks the contract: it stops here instead of piling up on the gate.
    public async Task<SharedClientTelemetry.BatchResult> Accept(SharedClientTelemetry.Batch batch)
    {
        if (!_gate.Wait(0))
            return _stop;

        try
        {
            if (batch.SessionId != _current)
                return _stop;

            if (!_switch.IsEnabled)
            {
                await CloseCurrent(TelemetrySessionStatus.Ended);
                return _stop;
            }

            var isLive = await _orleans.GetGrain<IClientTelemetrySession>(batch.SessionId).Accept(TelemetryBatchParser.Parse(batch));

            if (!isLive)
                _current = null;

            return isLive ? _goOn : _stop;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("[ClientTelemetry] A batch is not taken: {Failure}", exception.GetType().Name);
            return _stop;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EmptyResponse> End(SharedClientTelemetry.SessionEnd request)
    {
        await _gate.WaitAsync();

        try
        {
            if (request.SessionId == _current)
                await CloseCurrent(TelemetrySessionStatus.Ended);

            return EmptyResponse.Ok;
        }
        finally
        {
            _gate.Release();
        }
    }

    // The socket is gone, which counts as a crash of the client: no retry and no reconnect can continue this session.
    public async Task OnDisconnected()
    {
        await _gate.WaitAsync();

        try
        {
            _isDisconnected = true;
            _startups.Close();
            await CloseCurrent(TelemetrySessionStatus.Lost);
        }
        finally
        {
            _gate.Release();
        }
    }

    // A failed write stays with the grain: its buffer is still live, so the timer repeats the close with this status.
    private async Task CloseCurrent(TelemetrySessionStatus status)
    {
        if (_current is not { } id)
            return;

        _current = null;

        try
        {
            await _orleans.GetGrain<IClientTelemetrySession>(id).Close(status);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("[ClientTelemetry] The close of the session {Session} failed: {Failure}", id, exception.GetType().Name);
        }
    }
}
