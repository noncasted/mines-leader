using Benchmarks;
using Cluster.Configs;
using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.Monitoring;
using Cluster.State;
using Common;
using Common.Extensions;
using Game.Global;
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

            // Project services
            builder.Add<ClusterConfigsSetup>()
                   .As<ICoordinatorSetupCompleted>();

            builder.Add<ClusterBotsSetup>()
                   .As<ICoordinatorSetupCompleted>();

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
                .AddBase(ServiceTag.Meta)
                .ConfigureCors();

            // Project services
            builder
                .AddBackendMatchServices();

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
                .AddBase(ServiceTag.Game)
                .ConfigureCors();

            // Project services
            builder
                .AddGlobalSessions();

            builder.Services
                   .AddOpenApi()
                   .AddCors();

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
                .AddBase(ServiceTag.Silo);

            builder.Add<SideEffectsWorker>()
                   .As<IHostedService>();

            builder.Services.Add<SideEffectsMonitorService>()
                   .As<ILocalSetupCompleted>();

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
                .AddBase(ServiceTag.Console)
                .AddBlazorComponents();

            // HTTP clients for cross-gateway monitoring
            builder.Services.AddHttpClient("game", c => c.BaseAddress = new Uri("http://game"));
            builder.Services.AddHttpClient("meta", c => c.BaseAddress = new Uri("http://meta"));

            // Console-only services
            builder.Services.AddSingleton<IMatchHistoryStorage, MatchHistoryStorage>();
            builder.Services.AddSingleton<ICardAnalyticsStorage, CardAnalyticsStorage>();

            // Project services — auto-discover all IClusterTest implementations in Tests assembly
            var testsAssembly = typeof(IClusterTest).Assembly;

            foreach (var type in testsAssembly.GetTypes())
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

        private IHostApplicationBuilder AddBase(ServiceTag serviceTag)
        {
            if (builder is WebApplicationBuilder webBuilder)
                webBuilder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

            builder.Services.AddHostedService<ClusterParticipantStartup>();

            builder.Add<ClusterParticipantContext>()
                   .As<IClusterParticipantContext>();

            builder
                .AddEnvironment(serviceTag)
                .AddServiceLoop()
                .AddMessaging()
                .AddOrleansUtils()
                .AddServiceDiscovery()
                .AddTaskScheduling()
                .AddClusterFeatures()
                .AddMemoryPack()
                .AddTests()
                .AddConfigs()
                .AddSideEffects()
                .AddStates()
                .AddMonitoring()
                .AddUserServices();

            builder.AddBotServices();

            builder.Add<DbSource>()
                   .As<IDbSource>();

            builder.Services.AddHostedService<MetricsSnapshotService>();

            return builder;
        }

        private IHostApplicationBuilder AddStates() {
            var states = new List<GrainStateInfo>();
            GeneratedStatesRegistration.AddAllStates(states);
            var registry = new GrainStatesRegistry(states);
            builder.Add(registry).As<IGrainStatesRegistry>();
            return builder;
        }

        private IHostApplicationBuilder AddSideEffects()
        {
            builder.Add<SideEffectsStorage>()
                   .As<ISideEffectsStorage>();

            return builder;
        }

        private IHostApplicationBuilder AddMemoryPack()
        {
            var entityPayloads = new UnionBuilder<IEntityPayload>();
            var contexts = new UnionBuilder<INetworkContext>();

            entityPayloads
                .Add<MenuPlayerPayload>()
                .Add<CardCreatePayload>()
                .Add<PlayerCreatePayload>();

            contexts
                .Add<EmptyResponse>()
                .AddSharedBackend()
                .AddSharedGame()
                .AddSharedSession();

            contexts.Build();
            entityPayloads.Build();

            return builder;
        }

        private IHostApplicationBuilder AddBlazorComponents()
        {
            builder.Services
                   .AddRazorComponents()
                   .AddInteractiveServerComponents();

            return builder;
        }

        private IHostApplicationBuilder AddTests()
        {
            builder.Add<BenchmarkRunner>();
            builder.Add<BenchmarkStorage>();
            builder.Add<ClusterTestUtils>();

            // Auto-discover all BenchmarkNode<> subclasses in Tests assembly
            var testsAssembly = typeof(IClusterTest).Assembly;

            foreach (var type in testsAssembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!IsTestNodeType(type))
                    continue;

                builder.Services.AddSingleton(type);
                builder.Services.AddSingleton(typeof(ICoordinatorSetupCompleted), sp => sp.GetRequiredService(type));
            }

            builder.Add<StateMigrationTest.MigrationTestStep_V0>()
                   .As<IStateMigrationStep>();

            builder.Add<StateMigrationTest.MigrationTestStep_V1>()
                   .As<IStateMigrationStep>();

            return builder;
        }

        private static bool IsTestNodeType(Type type)
        {
            var current = type.BaseType;

            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BenchmarkNode<>))
                    return true;
                current = current.BaseType;
            }

            return false;
        }
    }
}