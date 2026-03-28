using Cluster.Configs;
using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Game.Global;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.State;
using Meta.Bots;
using Meta.Matches;
using Meta.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using Tests;

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

            // Project services
            
            // Project services — auto-discover all IClusterTest implementations in Tests assembly
            var testsAssembly = typeof(IClusterTest).Assembly;
            foreach (var type in testsAssembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IClusterTest).IsAssignableFrom(type)) continue;

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
                .AddUserServices();

            builder.AddBotServices();

            builder.Add<DbSource>()
                .As<IDbSource>();

            return builder;
        }

        private IHostApplicationBuilder AddStates()
        {
            var states = new List<GrainStateInfo>();

            Add<StateTest.TestState>(StatesLookup.StateTestTest);
            Add<StateMigrationTest.MigrationTestState_0>(StatesLookup.StateMigrationTest);
            Add<StateMigrationTest.MigrationTestState_1>(StatesLookup.StateMigrationTest);
            Add<TransactionTestState>(StatesLookup.TransactionTest);
            Add<UserState>(StatesLookup.User);
            Add<UserAuthState>(StatesLookup.UserAuth);
            Add<UserProgressionState>(StatesLookup.UserProgression);
            Add<UserProjectionState>(StatesLookup.UserProjection);
            Add<UserMatchHistoryState>(StatesLookup.UserMatchHistory);
            Add<UserDeckState>(StatesLookup.UserDeck);
            Add<MatchState>(StatesLookup.Match);
            Add<BotState>(StatesLookup.Bot);
            Add<BotConfigOptions>(StatesLookup.BotConfig);
            Add<CardConfigOptions>(StatesLookup.CardConfig);
            Add<GameModeOptions>(StatesLookup.GameModeConfig);
            Add<RatingOptions>(StatesLookup.RatingConfig);
            Add<SideEffectsOptions>(StatesLookup.SideEffectsConfig);
            Add<MessageQueueOptions>(StatesLookup.MessageQueueConfig);
            Add<TaskBalancerOptions>(StatesLookup.TaskBalancerConfig);
            Add<UserRatingState>(StatesLookup.UserRating);

            var registry = new GrainStatesRegistry(states);

            builder.Add(registry)
                .As<IGrainStatesRegistry>();

            return builder;

            void Add<T>(StatesLookup.Info lookupInfo)
            {
                var info = new GrainStateInfo
                {
                    TableName = lookupInfo.TableName,
                    KeyType = lookupInfo.KeyType,
                    Type = typeof(T),
                    Name = lookupInfo.StateName
                };

                states.Add(info);
            }
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
            builder.Add<ClusterTestUtils>();

            // Auto-discover all ClusterTestNode<> subclasses in Tests assembly
            var testsAssembly = typeof(IClusterTest).Assembly;
            foreach (var type in testsAssembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!IsTestNodeType(type)) continue;

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
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ClusterTestNode<>))
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }
}