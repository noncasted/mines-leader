using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Internal;

namespace Internal.Tests
{
    internal static class VContainerBenchmarkHost
    {
        public static BenchmarkReport Run()
        {
            return ContainerBenchmarkRunner.Run("VContainer", Open);
        }

        public static IBenchmarkSession Open()
        {
            var rootBuilder = new VContainer.ContainerBuilder();
            BenchmarkGraph.Apply(BenchmarkScopeLevel.Root, new VContainerBenchmarkRegistry(rootBuilder));
            var root = rootBuilder.Build();

            var match = root.CreateScope(builder => {
                BenchmarkGraph.Apply(BenchmarkScopeLevel.Match, new VContainerBenchmarkRegistry(builder));
            });

            var card = match.CreateScope(builder => {
                BenchmarkGraph.Apply(BenchmarkScopeLevel.Card, new VContainerBenchmarkRegistry(builder));
            });

            return new VContainerBenchmarkSession(root, match, card);
        }

        private sealed class VContainerBenchmarkRegistry : IBenchmarkRegistry
        {
            private readonly IContainerBuilder _builder;

            public VContainerBenchmarkRegistry(IContainerBuilder builder)
            {
                _builder = builder;
            }

            public void Add(Type implementation, ServiceLifetime lifetime, Type[] markers)
            {
                var registration = _builder.Register(implementation, Map(lifetime)).AsSelf();

                for (var i = 0; i < markers.Length; i++)
                    registration.As(markers[i]);
            }

            private static VContainer.Lifetime Map(ServiceLifetime lifetime)
            {
                switch (lifetime)
                {
                    case ServiceLifetime.Transient:
                        return VContainer.Lifetime.Transient;
                    case ServiceLifetime.Scoped:
                        return VContainer.Lifetime.Scoped;
                    default:
                        return VContainer.Lifetime.Singleton;
                }
            }
        }

        private sealed class VContainerBenchmarkSession : IBenchmarkSession
        {
            private readonly IObjectResolver _root;
            private readonly IScopedObjectResolver _match;
            private readonly IScopedObjectResolver _card;

            public VContainerBenchmarkSession(
                IObjectResolver root,
                IScopedObjectResolver match,
                IScopedObjectResolver card)
            {
                _root = root;
                _match = match;
                _card = card;
            }

            public int ResolveAllPhases()
            {
                // Same consume path as EventLoop.ResolveList<T>.
                var count = 0;
                count += Phase<IBenchBaseSetup>(_card);
                count += Phase<IBenchBaseSetupAsync>(_card);
                count += Phase<IBenchSetup>(_card);
                count += Phase<IBenchSetupAsync>(_card);
                count += Phase<IBenchSetupCompletion>(_card);
                count += Phase<IBenchSetupCompletionAsync>(_card);
                count += Phase<IBenchLoaded>(_card);
                count += Phase<IBenchLoadedAsync>(_card);
                count += Phase<IBenchDispose>(_card);
                count += Phase<IBenchDisposeAsync>(_card);
                count += Phase<IBenchSceneService>(_card);
                count += Phase<IBenchEntityComponent>(_card);
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

            public object Resolve(BenchmarkScopeLevel level, Type type)
            {
                switch (level)
                {
                    case BenchmarkScopeLevel.Root:
                        return _root.Resolve(type);
                    case BenchmarkScopeLevel.Match:
                        return _match.Resolve(type);
                    default:
                        return _card.Resolve(type);
                }
            }

            public void Dispose()
            {
                _card.Dispose();
                _match.Dispose();
                _root.Dispose();
            }

            private static int Phase<T>(IObjectResolver resolver)
            {
                return resolver.Resolve<ContainerLocal<IReadOnlyList<T>>>().Value.Count;
            }
        }
    }
}