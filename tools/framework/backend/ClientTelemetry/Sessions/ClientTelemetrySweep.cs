using Common.Extensions;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;

namespace Game.ClientTelemetry;

// Runs in every silo; a second visit of a session changes nothing. Nothing else would come to a session a crash left open.
public sealed class ClientTelemetrySweep : ILocalSetupCompleted
{
    public ClientTelemetrySweep(
        ITelemetrySessionCollection sessions,
        ITelemetryStartupCollection startups,
        IOrleans orleans,
        ClientTelemetryStorageOptions options,
        TelemetrySessionClock time,
        ILogger<ClientTelemetrySweep> logger)
    {
        _sessions = sessions;
        _startups = startups;
        _orleans = orleans;
        _options = options;
        _time = time;
        _logger = logger;
    }

    private readonly ITelemetrySessionCollection _sessions;
    private readonly ITelemetryStartupCollection _startups;
    private readonly IOrleans _orleans;
    private readonly ClientTelemetryStorageOptions _options;
    private readonly TelemetrySessionClock _time;
    private readonly ILogger<ClientTelemetrySweep> _logger;

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        Loop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Run()
    {
        var expiredAt = _time.UtcNow - TimeSpan.FromDays(_options.RetentionDays);

        foreach (var (id, session) in _sessions.ToArray())
        {
            if (session.Closed && session.ClosedAt > expiredAt)
                continue;

            // The boundary of one session: the next ones and the next pass go on.
            try
            {
                await _orleans.GetGrain<IClientTelemetrySession>(id).Sweep();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "[ClientTelemetry] The sweep of the session {Session} failed", id);
            }
        }

        foreach (var (id, startup) in _startups.ToArray())
        {
            if (startup.UpdatedAt > expiredAt)
                continue;

            // The boundary of one record, as above.
            try
            {
                await _orleans.GetGrain<IClientTelemetryStartup>(id).Sweep();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "[ClientTelemetry] The sweep of the startup {Startup} failed", id);
            }
        }
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
        var delay = _options.SweepFirstDelay;

        while (lifetime.IsTerminated == false)
        {
            await Task.Delay(delay, lifetime.Token);
            await Run();
            delay = _options.SweepInterval;
        }
    }
}
