using Cluster.Configs;
using Cluster.Coordination;
using Cluster.Discovery;
using Cluster.State;
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
                .AddStateAttributes()
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

            builder.Add<StateFactory>()
                .As<IStateFactory>();

            builder.Add<StateAttributeMapper>()
                .As<IAttributeToFactoryMapper<StateAttribute>>();

            builder.Add<GrainStateStorage>()
                .As<IGrainStateStorage>();

            builder.Add<StateSerializer>()
                .As<IStateSerializer>();

            builder.Add<Transactions>()
                .As<ITransactions>();


            return builder;
        }

        private IHostApplicationBuilder AddStates()
        {
            var states = new List<GrainStateInfo>();

            Add<StateTest.TestState>("state_test_default_state", GrainKeyType.String);
            Add<TransactionTestState>("state_test_transactional_state", GrainKeyType.Guid);
            // Add<UserState>("state_user_entity", GrainKeyType.Guid);
            // Add<UserAuthState>("state_user_auth", GrainKeyType.Guid);
            // Add<UserProgressionState>("state_user_progression", GrainKeyType.Guid);
            // Add<UserMatchHistoryState>("state_user_match_history", GrainKeyType.Guid);
            // Add<UserDeckState>("state_user_projection", GrainKeyType.Guid);
            // Add<MatchState>("state_match_entity", GrainKeyType.Guid);
            // Add<MessageQueueState>("state_message_queue", GrainKeyType.Guid);

            var lookup = new HashSet<string>();

            foreach (var stateInfo in states)
            {
                if (lookup.Add(stateInfo.TableName) == false)
                    throw new Exception($"Duplicate state table name: {stateInfo.TableName}");
            }

            var registry = new GrainStatesRegistry(states);

            builder.Add(registry)
                .As<IGrainStatesRegistry>();

            return builder;

            void Add<T>(string tableName, GrainKeyType keyType)
            {
                var info = new GrainStateInfo
                {
                    TableName = tableName,
                    KeyType = keyType,
                    Type = typeof(T)
                };

                states.Add(info);
            }
        }

        private IHostApplicationBuilder AddSideEffects()
        {
            builder.Add<SideEffectsStorage>()
                .As<ISideEffectsStorage>();

            builder.Add<SideEffectsWorker>()
                .As<ICoordinatorSetupCompleted>();

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

            return builder;
        }
    }
}