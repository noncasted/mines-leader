namespace Internal.Tests
{
    // Тот же граф, что BenchmarkGraph.Registrations, но статическими вызовами: генератор
    // строит класс скоупа только по коду installer'а. Меняешь таблицу — меняй и здесь
    // (сверку держит GeneratedContainer_Roots_MatchTable).
    internal static class BenchmarkRoots
    {
        [ContainerGraphRoot]
        public static void Root(IBuilder builder)
        {
            builder.Register<BenchRootTime>();
            builder.Register<BenchRootLog>().As<IBenchBaseSetup>();
            builder.Register<BenchRootConfig>();
            builder.Register<BenchRootAssets>().As<IBenchSceneService>();
            builder.Register<BenchRootPrefabs>();
            builder.Register<BenchRootAudio>().As<IBenchLoaded>();
            builder.Register<BenchRootInput>();
            builder.Register<BenchRootSaves>().As<IBenchDispose>();
            builder.Register<BenchRootPublisher>().As<IBenchSetup>();
            builder.Register<BenchRootUpdater>().As<IBenchSetup>().As<IBenchDispose>();
            builder.Register<BenchRootNetwork>();
            builder.Register<BenchRootProfiler>().As<IBenchBaseSetup>();
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(BenchmarkRoots), nameof(Root))]
        public static void Match(IBuilder builder)
        {
            builder.Register<BenchMatchRandom>();
            builder.Register<BenchMatchEvents>().As<IBenchSetup>();
            builder.Register<BenchMatchClock>().As<IBenchBaseSetup>();
            builder.Register<BenchMatchState>(ServiceLifetime.Scoped).As<IBenchSetup>();
            builder.Register<BenchMatchBoard>(ServiceLifetime.Scoped).As<IBenchSetup>().As<IBenchSceneService>();
            builder.Register<BenchMatchRules>(ServiceLifetime.Scoped);
            builder.Register<BenchMatchPlayers>(ServiceLifetime.Scoped).As<IBenchLoaded>();
            builder.Register<BenchMatchCards>(ServiceLifetime.Scoped).As<IBenchSetup>();
            builder.Register<BenchMatchScore>(ServiceLifetime.Scoped);
            builder.Register<BenchMatchSync>(ServiceLifetime.Scoped).As<IBenchSetupAsync>();
            builder.Register<BenchMatchLoop>(ServiceLifetime.Scoped).As<IBenchSetup>().As<IBenchLoaded>();
            builder.Register<BenchMatchCamera>(ServiceLifetime.Scoped).As<IBenchSceneService>();
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(BenchmarkRoots), nameof(Match))]
        public static void Card(IBuilder builder)
        {
            builder.Register<BenchCardId>().As<IBenchEntityComponent>();
            builder.Register<BenchCardDefinition>();
            builder.Register<BenchCardOwner>().As<IBenchEntityComponent>();
            builder.Register<BenchCardView>(ServiceLifetime.Transient).As<IBenchEntityComponent>().As<IBenchSetup>();
            builder.Register<BenchCardState>(ServiceLifetime.Transient);
            builder.Register<BenchCardAction>(ServiceLifetime.Transient).As<IBenchSetup>();
            builder.Register<BenchCardAnimator>(ServiceLifetime.Transient).As<IBenchEntityComponent>();
            builder.Register<BenchCardInput>(ServiceLifetime.Transient).As<IBenchSetup>();
        }
    }
}