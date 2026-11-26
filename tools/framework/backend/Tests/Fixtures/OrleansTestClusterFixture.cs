using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using Internal;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Orleans.TestingHost;
using Xunit;
using TaskScheduler = Infrastructure.Execution.TaskScheduler;

namespace Framework.Tests.Fixtures;

// Base fixture for Orleans integration tests: an in-process TestCluster over a real PostgreSQL database, with the
// framework services a silo registers (states, transactions, side effects, messaging, task scheduling) and the test
// grains of this assembly.
public class OrleansTestClusterFixture : IAsyncLifetime
{
    private InProcessTestCluster _cluster = null!;
    private Lifetime? _messagingLifetime;

    public InProcessTestCluster Cluster => _cluster;
    public IGrainFactory GrainFactory => _cluster.Client;
    public DatabaseFixture Database { get; } = new();

    protected virtual int SiloCount => 1;

    protected virtual void ConfigureSiloServices(IServiceCollection services)
    {
    }

    protected virtual void ConfigureSilo(ISiloBuilder siloBuilder)
    {
    }

    public virtual async ValueTask InitializeAsync()
    {
        await Database.Initialize();

        var builder = new InProcessTestClusterBuilder((short)SiloCount);

        var dataSource = Database.DataSource;

        builder.ConfigureSilo((options, siloBuilder) => {
            siloBuilder.AddMemoryGrainStorage("Default");

            siloBuilder.AddGrainExtension<IGrainTransactionHandler, GrainTransactionHandler>();

            siloBuilder.ConfigureServices(services => {
                // Database source
                var dbSource = Substitute.For<IDbSource>();
                dbSource.Value.Returns(dataSource);
                services.AddSingleton(dbSource);

                // Marten document store for event sourcing tests: the hosts' settings over the test registry.
                var registry = TestStates.CreateRegistry();

                var martenStore = DocumentStore.For(options => {
                    options.Connection(dataSource);
                    options.AutoCreateSchemaObjects = AutoCreate.All;
                    TestStates.ConfigureMarten(options, registry);
                });
                services.AddSingleton<IDocumentStore>(martenStore);

                // State registry: the framework states and the test grain states
                services.AddSingleton<IGrainStatesRegistry>(registry);

                // Core Orleans utilities
                services.AddSingleton<IStateSerializer, StateSerializer>();
                services.AddSingleton<IStateMigrations, StateMigrations>();
                services.AddSingleton<DirectStorage>();
                services.AddSingleton<IStateStorage, StateStorage>();
                services.AddSingleton<IEventStorage, EventStorage>();
                services.AddSingleton(provider => new Lazy<ITransactionConfig>(provider.GetRequiredService<ITransactionConfig>));
                services.AddSingleton<ITransactions, Transactions>();
                services.AddSingleton<IOrleans, OrleansUtils>();
                services.AddSingleton<ISideEffectsStorage, SideEffectsStorage>();

                // State factory for grain [State] attribute injection
                services.AddSingleton<IStateFactory, StateFactory>();
                services.AddSingleton<IAttributeToFactoryMapper<StateAttribute>, StateAttributeMapper>();

                // Event state factory for grain [EventState] attribute injection
                services.AddSingleton<IEventStateFactory, EventStateFactory>();
                services.AddSingleton<IAttributeToFactoryMapper<EventStateAttribute>, EventStateAttributeMapper>();

                // Migration steps (V0→V1 chain targeting MigrationTestState_1)
                services.AddSingleton<IStateMigrationStep, Grains.MigrationTestStep_V0>();
                services.AddSingleton<IStateMigrationStep, Grains.MigrationTestStep_V1>();

                // Migration steps (V0→V1→V2 chain targeting MigrationTestState_2)
                services.AddSingleton<IStateMigrationStep, Grains.MigrationV2TestStep_V0>();
                services.AddSingleton<IStateMigrationStep, Grains.MigrationV2TestStep_V1>();
                services.AddSingleton<IStateMigrationStep, Grains.MigrationTestStep_V2>();

                // StateCollection utilities for tests
                services.AddSingleton(typeof(StateCollectionUtils<,>));

                // Messaging
                services.AddSingleton<IMessaging, Infrastructure.Messaging>();
                services.AddSingleton<IDurableQueueClient, DurableQueueClient>();
                services.AddSingleton<IRuntimePipeClient, RuntimePipeClient>();
                services.AddSingleton<IRuntimeChannelClient, RuntimeChannelClient>();

                // Service environment
                services.AddSingleton<IServiceEnvironment>(new ServiceEnvironment
                {
                    Tag = TestRoles.Silo
                });

                // Service loop — mock as started
                var loopObserver = Substitute.For<IServiceLoopObserver>();
                loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));
                services.AddSingleton(loopObserver);

                // Cluster participant context — mock as initialized
                var participantContext = Substitute.For<IClusterParticipantContext>();
                participantContext.IsInitialized.Returns(new SharedViewableProperty<bool>(true));
                services.AddSingleton(participantContext);

                // Cluster flags — all enabled
                var clusterFlags = Substitute.For<IClusterFlags>();
                clusterFlags.SideEffectsEnabled.Returns(true);
                services.AddSingleton(clusterFlags);

                // Configs — the values the hosts of the projects run with, through TestAddressableState
                RegisterTestConfigs(services);

                // Task scheduling
                services.AddSingleton<ITaskScheduler, TaskScheduler>();
                services.AddSingleton<ITaskQueue, TaskQueue>();
                services.AddSingleton<ITaskBalancer, TaskBalancer>();

                // Custom silo services from derived fixtures
                ConfigureSiloServices(services);
            });

            ConfigureSilo(siloBuilder);
        });

        _cluster = builder.Build();
        await _cluster.DeployAsync();

        // Start messaging (resubscribe loops) — in production this is done by ServiceLoop
        var messaging = _cluster.Silos[0].ServiceProvider.GetRequiredService<IMessaging>();
        var testLifetime = new Lifetime();
        await messaging.Start(testLifetime);
        _messagingLifetime = testLifetime;
    }

    private static void RegisterTestConfigs(IServiceCollection services)
    {
        // The worker scans fast enough for the tests to drain within their waits.
        RegisterConfig<ISideEffectsConfig, SideEffectsOptions>(services, new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 500
        });

        RegisterConfig<ITransactionConfig, TransactionOptions>(services, new TransactionOptions
        {
            LockWaitSeconds = 2f,
            StuckGraceSeconds = 5f
        });

        RegisterConfig<IDurableQueueConfig, DurableQueueOptions>(services);
        RegisterConfig<ITaskBalancerConfig, TaskBalancerOptions>(services);
        // A timed-out request of the pipe tests ends within their waits, a request without a handler gives up in 0.35 s.
        RegisterConfig<IRuntimePipeConfig, RuntimePipeOptions>(services, new RuntimePipeOptions
        {
            SendTimeoutSeconds = 2,
            SendRetryBaseDelayMs = 50
        });
        RegisterConfig<IRuntimeChannelConfig, RuntimeChannelOptions>(services);

        // Cluster features
        var features = new TestAddressableState<ClusterFeaturesState>();
        var clusterFeatures = Substitute.For<IClusterFeatures>();
        clusterFeatures.Value.Returns(features.Value);
        clusterFeatures.IsInitialized.Returns(true);
        clusterFeatures.SideEffectsEnabled.Returns(true);
        services.AddSingleton(clusterFeatures);
    }

    private static void RegisterConfig<TInterface, TOptions>(
        IServiceCollection services,
        TOptions? value = null)
        where TInterface : class, IAddressableState<TOptions>
        where TOptions : class, new()
    {
        var state = new TestAddressableState<TOptions>(value ?? new TOptions());
        var mock = Substitute.For<TInterface>();
        mock.Value.Returns(state.Value);
        mock.IsInitialized.Returns(true);
        services.AddSingleton(mock);
    }

    public virtual async ValueTask DisposeAsync()
    {
        _messagingLifetime?.Terminate();
        await _cluster.StopAllSilosAsync();
        await Database.DisposeAsync();
    }
}
