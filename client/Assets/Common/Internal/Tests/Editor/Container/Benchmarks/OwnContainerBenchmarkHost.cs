using System;

namespace Internal.Tests
{
    internal static class OwnContainerBenchmarkHost
    {
        public static BenchmarkReport Run()
        {
            return ContainerBenchmarkRunner.Run("Own", Open);
        }

        public static IBenchmarkSession Open()
        {
            var rootBuilder = new ContainerBuilder("Root");
            BenchmarkGraph.Apply(BenchmarkScopeLevel.Root, new OwnBenchmarkRegistry(rootBuilder));
            var root = rootBuilder.Build();

            var matchBuilder = root.CreateChild();
            BenchmarkGraph.Apply(BenchmarkScopeLevel.Match, new OwnBenchmarkRegistry(matchBuilder));
            var match = matchBuilder.Build();

            var cardBuilder = match.CreateChild();
            BenchmarkGraph.Apply(BenchmarkScopeLevel.Card, new OwnBenchmarkRegistry(cardBuilder));
            var card = cardBuilder.Build();

            return new OwnBenchmarkSession(root, match, card);
        }

        private sealed class OwnBenchmarkRegistry : IBenchmarkRegistry
        {
            private readonly IContainerRegistry _builder;

            public OwnBenchmarkRegistry(IContainerRegistry builder)
            {
                _builder = builder;
            }

            public void Add(Type implementation, ServiceLifetime lifetime, Type[] markers)
            {
                var registration = _builder.Add(implementation, lifetime).AsSelf();
                for (var i = 0; i < markers.Length; i++)
                    registration.As(markers[i]);
            }
        }

        private sealed class OwnBenchmarkSession : IBenchmarkSession
        {
            private readonly IContainer _root;
            private readonly IContainer _match;
            private readonly IContainer _card;

            public OwnBenchmarkSession(IContainer root, IContainer match, IContainer card)
            {
                _root = root;
                _match = match;
                _card = card;
            }

            public int ResolveAllPhases()
            {
                var count = 0;
                count += _card.ResolveAll<IBenchBaseSetup>().Count;
                count += _card.ResolveAll<IBenchBaseSetupAsync>().Count;
                count += _card.ResolveAll<IBenchSetup>().Count;
                count += _card.ResolveAll<IBenchSetupAsync>().Count;
                count += _card.ResolveAll<IBenchSetupCompletion>().Count;
                count += _card.ResolveAll<IBenchSetupCompletionAsync>().Count;
                count += _card.ResolveAll<IBenchLoaded>().Count;
                count += _card.ResolveAll<IBenchLoadedAsync>().Count;
                count += _card.ResolveAll<IBenchDispose>().Count;
                count += _card.ResolveAll<IBenchDisposeAsync>().Count;
                count += _card.ResolveAll<IBenchSceneService>().Count;
                count += _card.ResolveAll<IBenchEntityComponent>().Count;
                return count;
            }

            public object ResolveSingleton()
            {
                return _card.Resolve<BenchRootTime>();
            }

            public object ResolveTransient()
            {
                return _card.Resolve<BenchCardAction>();
            }

            public void Dispose()
            {
                _card.Dispose();
                _match.Dispose();
                _root.Dispose();
            }
        }
    }
}
