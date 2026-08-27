using System.Reflection;
using Cluster.Configs;
using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using JasperFx;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;
using Marten.Services;
using Meta.Bots;
using Meta.Users;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Orleans.TestingHost;
using Shared;
using Tests.Grains;
using Xunit;
using TaskScheduler = Infrastructure.Execution.TaskScheduler;

namespace Tests.Fixtures;

/// <summary>
/// Base fixture for Orleans integration tests.
/// Spins up an in-process TestCluster with a real PostgreSQL database.
/// </summary>
public class OrleansTestClusterFixture : IAsyncLifetime
{
    private InProcessTestCluster _cluster = null!;

    public InProcessTestCluster Cluster => _cluster;
    public IGrainFactory GrainFactory => _cluster.Client;
    public DatabaseFixture Database { get; } = new();

    protected virtual int SiloCount => 1;

    /// <summary>
    /// Override to register additional silo-level services (e.g. game dependencies).
    /// </summary>
    protected virtual void ConfigureSiloServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Override to configure ISiloBuilder (e.g. grain extensions).
    /// </summary>
    protected virtual void ConfigureSilo(ISiloBuilder siloBuilder)
    {
    }

    public virtual async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();

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

                // Marten document store for event sourcing tests
                var martenStore = DocumentStore.For(options =>
                {
                    options.Connection(dataSource);
                    options.Events.StreamIdentity = StreamIdentity.AsString;
                    options.Events.AppendMode = EventAppendMode.Quick;
                    options.AutoCreateSchemaObjects = AutoCreate.All;

                    var baseSettings = JsonStateSettings.CreateBase();
                    var jsonSerializer = new JsonNetSerializer();
                    jsonSerializer.Configure(s =>
                    {
                        s.TypeNameHandling = baseSettings.TypeNameHandling;
                        s.MetadataPropertyHandling = baseSettings.MetadataPropertyHandling;
                        s.PreserveReferencesHandling = baseSettings.PreserveReferencesHandling;
                        s.DateFormatHandling = baseSettings.DateFormatHandling;
                        s.DefaultValueHandling = baseSettings.DefaultValueHandling;
                        s.MissingMemberHandling = baseSettings.MissingMemberHandling;
                        s.NullValueHandling = baseSettings.NullValueHandling;
                        s.ConstructorHandling = baseSettings.ConstructorHandling;
                        s.TypeNameAssemblyFormatHandling = baseSettings.TypeNameAssemblyFormatHandling;
                        s.Formatting = baseSettings.Formatting;
                        foreach (var converter in baseSettings.Converters)
                            s.Converters.Add(converter);
                    });
                    options.Serializer(jsonSerializer);
                    var eventStateInterface = typeof(IEventStateValue);
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));
                    foreach (var asm in assemblies)
                    {
                        Type[] types;
                        try { types = asm.GetTypes(); }
                        catch { continue; }

                        foreach (var type in types)
                        {
                            if (type.IsInterface || type.IsAbstract || type.IsGenericTypeDefinition)
                                continue;
                            if (!eventStateInterface.IsAssignableFrom(type))
                                continue;
                            if (type.GetConstructor(Type.EmptyTypes) is not { IsPublic: true })
                                continue;

                            var snapshotMethod = options.Projections.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .FirstOrDefault(m => m.Name == "Snapshot"
                                    && m.IsGenericMethod
                                    && m.GetParameters().Length >= 1
                                    && m.GetParameters()[0].ParameterType == typeof(SnapshotLifecycle));
                            if (snapshotMethod != null)
                            {
                                var generic = snapshotMethod.MakeGenericMethod(type);
                                generic.Invoke(options.Projections, [SnapshotLifecycle.Inline, null]);
                            }
                        }
                    }
                });
                services.AddSingleton<IDocumentStore>(martenStore);

                // State registry — register all grain states
                services.AddSingleton<IGrainStatesRegistry>(BuildStatesRegistry());

                // Core Orleans utilities
                services.AddSingleton<IStateSerializer, StateSerializer>();
                services.AddSingleton<IStateMigrations, StateMigrations>();
                services.AddSingleton<DirectStorage>();
                services.AddSingleton<IStateStorage, StateStorage>();
                services.AddSingleton<IEventStorage, EventStorage>();
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
                services.AddSingleton<IStateMigrationStep, MigrationTestStep_V0>();
                services.AddSingleton<IStateMigrationStep, MigrationTestStep_V1>();

                // Migration steps (V0→V1→V2 chain targeting MigrationTestState_2)
                services.AddSingleton<IStateMigrationStep, MigrationV2TestStep_V0>();
                services.AddSingleton<IStateMigrationStep, MigrationV2TestStep_V1>();
                services.AddSingleton<IStateMigrationStep, MigrationTestStep_V2>();

                // StateCollection utilities for tests
                services.AddSingleton(typeof(StateCollectionUtils<,>));

                // Mock StateCollections for domain grains
                services.AddSingleton(Substitute.For<IUserCollection>());
                services.AddSingleton(Substitute.For<IBotCollection>());

                // Messaging
                services.AddSingleton<IMessaging, Infrastructure.Messaging>();
                services.AddSingleton<IDurableQueueClient, DurableQueueClient>();
                services.AddSingleton<IRuntimePipeClient, RuntimePipeClient>();
                services.AddSingleton<IRuntimeChannelClient, RuntimeChannelClient>();

                // Service environment
                services.AddSingleton<IServiceEnvironment>(new ServiceEnvironment
                {
                    IsDevelopment = true,
                    Tag = ServiceTag.Silo
                });

                // Service loop — mock as started
                var loopObserver = Substitute.For<IServiceLoopObserver>();
                loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));
                services.AddSingleton(loopObserver);

                // Cluster participant context — mock as initialized
                var participantContext = Substitute.For<IClusterParticipantContext>();
                participantContext.IsInitialized.Returns(new ViewableProperty<bool>(true));
                services.AddSingleton(participantContext);

                // Cluster flags — all enabled
                var clusterFlags = Substitute.For<IClusterFlags>();
                clusterFlags.MatchmakingEnabled.Returns(true);
                clusterFlags.SideEffectsEnabled.Returns(true);
                clusterFlags.SnapshotDiffGuardEnabled.Returns(true);
                services.AddSingleton(clusterFlags);

                // Configs — all with default values via TestAddressableState
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

    private Lifetime? _messagingLifetime;

    private static void RegisterTestConfigs(IServiceCollection services)
    {
        // Infrastructure configs — loaded from Orchestration/Coordinator JSON files
        RegisterConfig<ISideEffectsConfig, SideEffectsOptions>(services, "config.sideEffects");

        RegisterConfig<ITransactionConfig, TransactionOptions>(services, "config.transaction",
            o => {
                o.LockWaitSeconds = 2f;
                o.StuckGraceSeconds = 5f;
            });
        RegisterConfig<IDurableQueueConfig, DurableQueueOptions>(services, "config.durableQueue");
        RegisterConfig<ITaskBalancerConfig, TaskBalancerOptions>(services, "config.taskBalancer");

        // No JSON files for these — use defaults
        RegisterConfig<IRuntimePipeConfig, RuntimePipeOptions>(services);
        RegisterConfig<IRuntimeChannelConfig, RuntimeChannelOptions>(services);

        // Game configs — loaded from Orchestration/Coordinator JSON files
        RegisterConfig<ICardConfigs, CardConfigOptions>(services, "config.cards");
        RegisterConfig<IBotConfig, BotConfigOptions>(services, "config.bot");
        RegisterConfig<IGameModeConfig, GameModeOptions>(services, "config.gameMode");
        RegisterConfig<IMatchMakingConfig, MatchMakingOptions>(services, "config.matchMaking");
        RegisterConfig<IRatingConfig, RatingOptions>(services, "config.rating");
        RegisterConfig<IUserDeckConfig, UserDeckConfigOptions>(services, "config.userDeck");
        RegisterConfig<IPlayerConfig, PlayerConfigOptions>(services, "config.player");
        RegisterConfig<IInGameAchievementConfig, InGameAchievementOptions>(services, "config.achievements",
            value => value.Groups = InGameAchievementOptions.CreateDefault().Groups);

        // Cluster features
        var features = new TestAddressableState<ClusterFeaturesState>();
        var clusterFeatures = Substitute.For<IClusterFeatures>();
        clusterFeatures.Value.Returns(features.Value);
        clusterFeatures.IsInitialized.Returns(true);
        clusterFeatures.MatchmakingEnabled.Returns(true);
        clusterFeatures.SideEffectsEnabled.Returns(true);
        clusterFeatures.SnapshotDiffGuardEnabled.Returns(true);
        services.AddSingleton(clusterFeatures);
    }

    private static void RegisterConfig<TInterface, TOptions>(
        IServiceCollection services,
        string? jsonName = null,
        Action<TOptions>? configure = null)
        where TInterface : class, IAddressableState<TOptions>
        where TOptions : class, new()
    {
        var value = jsonName != null ? ConfigLoader.Load<TOptions>(jsonName) : new TOptions();
        configure?.Invoke(value);
        var state = new TestAddressableState<TOptions>(value);
        services.AddSingleton<TInterface>(CreateConfigMock<TInterface, TOptions>(state));
    }

    private static TInterface CreateConfigMock<TInterface, TOptions>(TestAddressableState<TOptions> state)
        where TInterface : class, IAddressableState<TOptions>
        where TOptions : class, new()
    {
        var mock = Substitute.For<TInterface>();
        mock.Value.Returns(state.Value);
        mock.IsInitialized.Returns(true);
        return mock;
    }

    private static GrainStatesRegistry BuildStatesRegistry()
    {
        var states = new List<GrainStateInfo>();
        GeneratedStatesRegistration.AddAllStates(states);
        return new GrainStatesRegistry(states);
    }

    public virtual async ValueTask DisposeAsync()
    {
        _messagingLifetime?.Terminate();
        await _cluster.StopAllSilosAsync();
        await Database.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection for Orleans integration tests.
/// Tests in this collection share a single TestCluster.
/// </summary>
[CollectionDefinition(nameof(OrleansIntegrationCollection))]
public class OrleansIntegrationCollection : ICollectionFixture<OrleansTestClusterFixture>;