using Benchmarks;
using Cluster.Configs;
using Cluster.Coordination;
using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using Cluster.Monitoring.Online;
using Cluster.State;
using Common;
using Common.Extensions;
using Game.Global;
using Game.MatchMetrics;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using Meta.Bots;
using Meta.Matches;
using Meta.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Shared;

namespace Orchestration;

public static class ProjectsSetupExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder SetupCoordinator()
        {
            // Basic services
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            // Cluster services
            builder
                .AddBase(ServiceTag.Coordinator);

            // Online statistics of the deploy (from ConnectedUsersLiveData): one tracker, on the coordinator only.
            builder.AddOnlineTracker();

            // Coordinator-specific readiness: deploy identity assigned by DeployIdentity BackgroundService
            builder.Services.AddHealthChecks()
                   .AddCheck<CoordinatorReadyHealthCheck>("coordinator-deploy", tags: ["ready"]);

            builder.Services.Configure<CoordinatorHealthOptions>(_ => { });

            // Project services
            builder.Add<ClusterConfigsSetup>()
                   .As<ICoordinatorSetupCompleted>();

            builder.Add<GameConfigsSetup>()
                   .As<ICoordinatorSetupCompleted>();

            builder.AddAddressableState<ClientTelemetryDefaultState>();

            builder.Add<ClientTelemetryDefaultSetup>()
                   .As<ICoordinatorSetupCompleted>();

            builder.Add<ClusterBotsSetup>()
                   .As<IServiceStarted>();

            return builder;
        }

        public IHostApplicationBuilder SetupMetaGateway()
        {
            // Basic services
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            // Cluster services
            builder
                .AddBase(GameRoles.Meta)
                .ConfigureCors();

            // Project services
            builder
                .AddBackendMatchServices()
                .AddMatchSummaries();

            builder.Services.AddOpenApi();

            return builder;
        }

        public IHostApplicationBuilder SetupGameGateway()
        {
            // Basic services
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            // Cluster services
            builder
                .AddBase(GameRoles.Game)
                .ConfigureCors();

            // Game servers publish their public url: matchmaking hands it to clients.
            builder.PublishGameServerUrl();

            // Project services
            builder
                .AddGlobalSessions();

            // The stream of the server of every match (SessionMetrics in the container of a session).
            builder.AddMatchMetricsWriter();

            builder.Services.AddOpenApi();

            return builder;
        }

        public IHostApplicationBuilder SetupSilo()
        {
            // Basic services
            builder
                .AddServiceDefaults()
                .ConfigureSilo();

            // Cluster services
            builder
                .AddBase(GameRoles.Silo)
                .AddMatchSummaries();

            builder.Add<SideEffectsWorker>()
                   .As<IHostedService>();

            return builder;
        }

        public IHostApplicationBuilder SetupConsole()
        {
            // Basic services
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            // Cluster services
            builder
                .AddBase(GameRoles.Console)
                .AddBlazorComponents();

            // HTTP clients for cross-gateway monitoring
            builder.Services.AddHttpClient("game", c => c.BaseAddress = new Uri("http://game"));
            builder.Services.AddHttpClient("meta", c => c.BaseAddress = new Uri("http://meta"));

            // Console-only services
            builder.AddMatchSummaries();

            // Benchmark roots (IClusterTest): only when the benchmarks are on (AddTests).
            if (BenchmarkSetupExtensions.ReadBenchmarksAvailability(builder).IsEnabled == false)
                return builder;

            foreach (var type in BenchmarksAvailability.Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!typeof(IClusterTest).IsAssignableFrom(type))
                    continue;

                builder.Services.AddSingleton(type);
                builder.Services.AddSingleton(typeof(IClusterTest), sp => sp.GetRequiredService(type));
            }

            return builder;
        }
    }
}
