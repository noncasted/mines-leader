using Cluster;
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
            builder.Services.Add<ClusterConfigsSetup>()
                .As<ICoordinatorSetupCompleted>();

            builder.Services.Add<ClusterBotsSetup>()
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
            {
              //  webBuilder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);
            }

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
                .AddConfigs();

            builder.AddBotServices();

            builder.Services.Add<DbSource>()
                .As<IDbSource>();

            builder.Services.Add<StateFactory>()
                .As<IStateFactory>();

            builder.Services.Add<StateAttributeMapper>()
                .As<IAttributeToFactoryMapper<StateAttribute>>();

            builder.Services.Add<GrainStateStorage>()
                .As<IGrainStateStorage>();

            builder.Services.Add<StateSerializer>()
                .As<IStateSerializer>();
            
            builder.Services.Add<ITransactions, Transactions>();

            builder.Services.AddSingleton<IGrainStatesRegistry, GrainStatesRegistry>(sp =>
            {
                var statesInfo = new List<GrainStateInfo>
                {
                    new()
                    {
                        Type = typeof(StateTest.TestState),
                        TableName = "test_state",
                        KeyType = GrainKeyType.String
                    },
                    new()
                    {
                        TableName = "test_transaction_state",
                        KeyType = GrainKeyType.Guid,
                        Type = typeof(TransactionTestState)
                    }
                };
                
                return new GrainStatesRegistry(statesInfo);
            });

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
            builder.Services.Add<ClusterTestUtils>();
            builder.AddClusterTestNode<MessagingDirectQueueStressTest.Node>();
            builder.AddClusterTestNode<MessagingTransactionalQueueStressTest.Node>();
            builder.AddClusterTestNode<MessagePipeSendStressTest.Node>();
            builder.AddClusterTestNode<MessagePipeSendResponseStressTest.Node>();

            return builder;
        }
    }
}