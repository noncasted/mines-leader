using System.Diagnostics;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.ClientTelemetry;

// The startup traces of one socket: an id counts only on the socket that was given it, so a client cannot touch a record of another
// one. Every call is a boundary: telemetry never fails a socket of a player. A closed socket changes nothing: its records stay as they are.
internal sealed class ClientTelemetryStartups
{
    public ClientTelemetryStartups(IClientTelemetrySwitch telemetrySwitch, IOrleans orleans, ILogger<ClientTelemetryConnection> logger)
    {
        _switch = telemetrySwitch;
        _orleans = orleans;
        _logger = logger;
    }

    private static readonly SharedClientTelemetry.StartupTraceResult _off = new() { Enabled = false };

    private readonly IClientTelemetrySwitch _switch;
    private readonly IOrleans _orleans;
    private readonly ILogger<ClientTelemetryConnection> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<Guid> _issued = [];

    private int _writes;
    private volatile bool _isClosed;

    // The end of the socket: a trace still in flight finishes, the next ones are not taken.
    public void Close()
    {
        _isClosed = true;
    }

    public async Task<SharedClientTelemetry.StartupTraceResult> Accept(SharedClientTelemetry.StartupTrace trace, ActivityTraceId backendTrace)
    {
        if (!_switch.IsEnabled)
            return _off;

        await _gate.WaitAsync();

        try
        {
            if (_isClosed)
                return _off;

            var isNew = trace.StartupId == Guid.Empty;

            if ((!isNew && !IsIssued(trace.StartupId)) || _writes >= ClientTelemetryLimits.MaxStartupWritesPerSocket)
                return _off;

            if (isNew && IssuedCount() >= ClientTelemetryLimits.MaxStartupsPerSocket)
                return _off;

            var id = isNew ? Guid.NewGuid() : trace.StartupId;

            _writes++;
            await _orleans.GetGrain<IClientTelemetryStartup>(id).Store(TelemetryStartupParser.Parse(trace) with { BackendTraceId = BackendTraceId(backendTrace) });

            lock (_issued)
                _issued.Add(id);

            return new SharedClientTelemetry.StartupTraceResult { Enabled = true, StartupId = id };
        }
        catch (Exception exception)
        {
            _logger.LogWarning("[ClientTelemetry] A startup trace is not taken: {Failure}", exception.GetType().Name);
            return _off;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? BackendTraceId(ActivityTraceId backendTrace)
    {
        return backendTrace == default ? null : backendTrace.ToHexString();
    }

    public TelemetryStartupLink Link(Guid id)
    {
        if (id == Guid.Empty)
            return new TelemetryStartupLink(null, false);

        return IsIssued(id) ? new TelemetryStartupLink(id, false) : new TelemetryStartupLink(null, true);
    }

    private bool IsIssued(Guid id)
    {
        lock (_issued)
            return _issued.Contains(id);
    }

    private int IssuedCount()
    {
        lock (_issued)
            return _issued.Count;
    }
}
