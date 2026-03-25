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
using MudBlazor.Services;
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
                .AddUserFactory()
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
                .AddClusterTests()
                .AddConfigs()
                .AddSideEffects()
                .AddStates();

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
            builder.Services.Configure<SideEffectsOptions>(
                builder.Configuration.GetSection("SideEffects")
            );

            builder.Add<SideEffectsStorage>()
                .As<ISideEffectsStorage>();

            builder.Add<SideEffectsWorker>()
                .As<IHostedService>();

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
                .AddMudServices()
                .AddRazorComponents()
                .AddInteractiveServerComponents();

            return builder;
        }

        private IHostApplicationBuilder AddClusterTests()
        {
            builder.Add<ClusterTestUtils>();
            builder.AddClusterTestNode<MessagingDirectQueueStressTest.Node>();
            builder.AddClusterTestNode<MessagingTransactionalQueueStressTest.Node>();
            builder.AddClusterTestNode<MessagePipeSendStressTest.Node>();
            builder.AddClusterTestNode<MessagePipeSendResponseStressTest.Node>();

            builder.Add<StateMigrationTest.MigrationTestStep_V0>()
                .As<IStateMigrationStep>();
            builder.Add<StateMigrationTest.MigrationTestStep_V1>()
                .As<IStateMigrationStep>();

            return builder;
        }
    }
}