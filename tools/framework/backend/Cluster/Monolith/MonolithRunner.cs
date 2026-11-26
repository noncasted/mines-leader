using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Cluster.Discovery;
using Common.Extensions;
using Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Monolith;

// Several hosts of the cluster in one process, each with its own container, port, content root and role, as the
// separate services have them; the discovery and the coordinator do not know the difference. The set of roles is the
// game's. The runner owns the stop of the process (the signals, the cancellation of the caller); the first host that
// fails or stops without being asked to stops the others and the process ends as a whole with its failure (fail fast;
// the healthcheck of the deploy checks /ready of every port).
public class MonolithRunner
{
    public MonolithRunner(IConfiguration configuration, IReadOnlyList<MonolithHost> hosts)
    {
        var duplicate = hosts.GroupBy(host => host.Tag).FirstOrDefault(group => group.Count() > 1);

        if (duplicate != null)
            throw new ArgumentException($"The monolith has the host {duplicate.Key} twice", nameof(hosts));

        _configuration = configuration;
        _hosts = hosts;
    }

    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<MonolithHost> _hosts;

    private static readonly PosixSignal[] ShutdownSignals = [PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGQUIT];

    // The configuration of the process: appsettings.json next to the binaries, the variables, the command line.
    public static MonolithRunner Create(string[] args, IReadOnlyList<MonolithHost> hosts)
    {
        var configuration = new ConfigurationBuilder()
                            .SetBasePath(AppContext.BaseDirectory)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddEnvironmentVariables()
                            .AddCommandLine(args)
                            .Build();

        return new MonolithRunner(configuration, hosts);
    }

    // Returns when the process is asked to stop: SIGTERM / SIGINT / SIGQUIT (the runner owns them, the hosts have
    // MonolithHostLifetime) or the cancellation of the caller. Throws the failure of the host that failed first, or
    // "host <tag> stopped by itself" when a host ended without being asked to.
    public async Task Run(CancellationToken cancellation)
    {
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

        // Cancel = true: the runtime does not end the process, the hosts stop gracefully and Run returns.
        var signals = ShutdownSignals.Select(signal => PosixSignalRegistration.Create(signal, context => {
                                         context.Cancel = true;
                                         shutdown.Cancel();
                                     }))
                                     .ToList();

        try
        {
            await RunUntilShutdown(shutdown.Token);
        }
        finally
        {
            foreach (var registration in signals)
                registration.Dispose();
        }
    }

    private async Task RunUntilShutdown(CancellationToken shutdown)
    {
        // Every host is built before any starts: a broken role fails the process before it serves anything.
        var apps = _hosts.Select(host => (host.Tag, App: Build(host))).ToList();

        await using var telemetry = MonolithTelemetry.Create(_configuration, apps.Select(app => app.App.Services).ToList());
        var logger = telemetry.Logs.CreateLogger<MonolithRunner>();

        // One handler for the background tasks of all roles, before any ClusterParticipantStartup would install the
        // logger of its own host (it keeps a handler that is there).
        var previousHandler = NoAwaitExceptions.Handler;
        NoAwaitExceptions.SetHandler(e => logger.LogCritical(e, "[NoAwait] Unobserved background task failed"));

        try
        {
            await RunAll(apps, logger, shutdown);
        }
        finally
        {
            NoAwaitExceptions.SetHandler(previousHandler);
        }
    }

    private WebApplication Build(MonolithHost host)
    {
        var options = MonolithHostOptions.Read(_configuration, host.Tag);

        // The command line wins over the variables the hosts share with the process.
        return host.Build(new WebApplicationOptions
        {
            ApplicationName = host.ApplicationName,
            ContentRootPath = options.ContentRoot,
            // The static web assets of every host are published into the wwwroot of the executable (its manifest
            // names them), not into the content root of the role: a web root under hosts/<role> serves them empty.
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Args =
            [
                $"--urls=http://+:{options.Port}",
                $"--{WebHostDefaults.HostingStartupAssembliesKey}={typeof(MonolithHostingStartup).Assembly.GetName().Name}",
                $"--{TelemetrySettings.ServiceNameKey}={host.Tag.Value.ToLowerInvariant()}",
                $"--{TelemetrySettings.ProcessProvidersKey}=true"
            ]
        });
    }

    private static async Task RunAll(
        IReadOnlyList<(ServiceTag Tag, WebApplication App)> apps,
        ILogger logger,
        CancellationToken shutdown)
    {
        // Cancelled by the shutdown of the process or by the first host that ends.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var failure = (Exception?)null;

        logger.LogInformation("[Monolith] Starting {Hosts}", string.Join(", ", apps.Select(app => app.Tag)));

        await Task.WhenAll(apps.Select(app => RunHost(app.Tag, app.App)));

        // The failure of the host that failed or began to stop by itself first, not the stop it caused in the others.
        if (failure != null)
            ExceptionDispatchInfo.Throw(failure);

        return;

        async Task RunHost(ServiceTag tag, WebApplication app)
        {
            try
            {
                // Resolved before the run: RunAsync disposes the services of the host when it returns.
                var backgroundServices = app.Services.GetServices<IHostedService>().OfType<BackgroundService>().ToList();
                var selfStop = (Exception?)null;

                // Taken when the host begins to stop, not when it has stopped: another host may fail on its absence
                // (a coordinator without the grains of the silo) and end first, the cause is the host that began first.
                using var stopping = app.Lifetime.ApplicationStopping.Register(() => {
                    if (stop.IsCancellationRequested)
                        return;

                    selfStop = SelfStopReason(tag, backgroundServices);
                    Interlocked.CompareExchange(ref failure, selfStop, null);
                });

                await app.RunAsync(stop.Token);

                if (selfStop != null)
                {
                    logger.LogCritical(selfStop, "[Monolith] Host {Host} stopped by itself, stopping the process", tag);
                    return;
                }

                logger.LogInformation("[Monolith] Host {Host} stopped", tag);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                // Stopped while starting: the process stops, the host had nothing to serve yet.
                logger.LogInformation("[Monolith] Host {Host} stopped while starting", tag);
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "[Monolith] Host {Host} failed, stopping the process", tag);
                Interlocked.CompareExchange(ref failure, e, null);
            }
            finally
            {
                stop.Cancel();
            }
        }
    }

    // Why a host stopped without being asked to. A failed BackgroundService stops its host
    // (BackgroundServiceExceptionBehavior.StopHost) and RunAsync returns normally: its failure is the reason, the host
    // only logged it. Otherwise the host called StopApplication itself (e.g. a coordinator that gave up its deploy).
    private static Exception SelfStopReason(ServiceTag tag, IReadOnlyList<BackgroundService> services)
    {
        var ended = services.FirstOrDefault(service => service.ExecuteTask is { IsFaulted: true } or { IsCanceled: true });

        if (ended?.ExecuteTask?.Exception is { } exception)
            return exception.InnerExceptions.Count == 1 ? exception.InnerExceptions[0] : exception;

        if (ended != null)
            return new InvalidOperationException(
                $"host {tag} stopped by itself: its background service {ended.GetType().Name} was cancelled");

        return new InvalidOperationException($"host {tag} stopped by itself");
    }
}
