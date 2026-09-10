using System;
using Internal;

namespace Internal.Tests
{
    internal enum BenchmarkScopeLevel
    {
        Root = 0,
        Match = 1,
        Card = 2
    }

    internal readonly struct BenchmarkRegistration
    {
        public readonly Type Implementation;
        public readonly ServiceLifetime Lifetime;
        public readonly BenchmarkScopeLevel Scope;
        public readonly Type[] Markers;

        public BenchmarkRegistration(
            Type implementation,
            ServiceLifetime lifetime,
            BenchmarkScopeLevel scope,
            params Type[] markers)
        {
            Implementation = implementation;
            Lifetime = lifetime;
            Scope = scope;
            Markers = markers ?? Array.Empty<Type>();
        }
    }

    internal interface IBenchmarkRegistry
    {
        void Add(Type implementation, ServiceLifetime lifetime, Type[] markers);
    }

    internal static class BenchmarkGraph
    {
        public const int ScopeDepth = 3;
        public const int ResolveIterations = 10000;

        public static readonly Type SingletonResolveType = typeof(BenchRootTime);
        public static readonly Type TransientResolveType = typeof(BenchCardAction);

        public static readonly Type[] Markers =
        {
            typeof(IBenchBaseSetup),
            typeof(IBenchBaseSetupAsync),
            typeof(IBenchSetup),
            typeof(IBenchSetupAsync),
            typeof(IBenchSetupCompletion),
            typeof(IBenchSetupCompletionAsync),
            typeof(IBenchLoaded),
            typeof(IBenchLoadedAsync),
            typeof(IBenchDispose),
            typeof(IBenchDisposeAsync),
            typeof(IBenchSceneService),
            typeof(IBenchEntityComponent)
        };

        // Same table is applied to VContainer and own container.
        // Shape mirrors production: 18 Singleton / 9 Scoped / 5 Transient,
        // Global → Match → Card, 12 marker collections.
        public static readonly BenchmarkRegistration[] Registrations =
        {
            new(typeof(BenchRootTime), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root),
            new(typeof(BenchRootLog), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchBaseSetup)),
            new(typeof(BenchRootConfig), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root),
            new(typeof(BenchRootAssets), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchSceneService)),
            new(typeof(BenchRootPrefabs), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root),
            new(typeof(BenchRootAudio), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchLoaded)),
            new(typeof(BenchRootInput), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root),
            new(typeof(BenchRootSaves), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchDispose)),
            new(typeof(BenchRootPublisher), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchSetup)),
            new(typeof(BenchRootUpdater), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchSetup), typeof(IBenchDispose)),
            new(typeof(BenchRootNetwork), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root),
            new(typeof(BenchRootProfiler), ServiceLifetime.Singleton, BenchmarkScopeLevel.Root, typeof(IBenchBaseSetup)),

            new(typeof(BenchMatchRandom), ServiceLifetime.Singleton, BenchmarkScopeLevel.Match),
            new(typeof(BenchMatchEvents), ServiceLifetime.Singleton, BenchmarkScopeLevel.Match, typeof(IBenchSetup)),
            new(typeof(BenchMatchClock), ServiceLifetime.Singleton, BenchmarkScopeLevel.Match, typeof(IBenchBaseSetup)),
            new(typeof(BenchMatchState), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchSetup)),
            new(typeof(BenchMatchBoard), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchSetup), typeof(IBenchSceneService)),
            new(typeof(BenchMatchRules), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match),
            new(typeof(BenchMatchPlayers), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchLoaded)),
            new(typeof(BenchMatchCards), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchSetup)),
            new(typeof(BenchMatchScore), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match),
            new(typeof(BenchMatchSync), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchSetupAsync)),
            new(typeof(BenchMatchLoop), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchSetup), typeof(IBenchLoaded)),
            new(typeof(BenchMatchCamera), ServiceLifetime.Scoped, BenchmarkScopeLevel.Match, typeof(IBenchSceneService)),

            new(typeof(BenchCardId), ServiceLifetime.Singleton, BenchmarkScopeLevel.Card, typeof(IBenchEntityComponent)),
            new(typeof(BenchCardDefinition), ServiceLifetime.Singleton, BenchmarkScopeLevel.Card),
            new(typeof(BenchCardOwner), ServiceLifetime.Singleton, BenchmarkScopeLevel.Card, typeof(IBenchEntityComponent)),
            new(typeof(BenchCardView), ServiceLifetime.Transient, BenchmarkScopeLevel.Card, typeof(IBenchEntityComponent), typeof(IBenchSetup)),
            new(typeof(BenchCardState), ServiceLifetime.Transient, BenchmarkScopeLevel.Card),
            new(typeof(BenchCardAction), ServiceLifetime.Transient, BenchmarkScopeLevel.Card, typeof(IBenchSetup)),
            new(typeof(BenchCardAnimator), ServiceLifetime.Transient, BenchmarkScopeLevel.Card, typeof(IBenchEntityComponent)),
            new(typeof(BenchCardInput), ServiceLifetime.Transient, BenchmarkScopeLevel.Card, typeof(IBenchSetup))
        };

        public static void Apply(BenchmarkScopeLevel scope, IBenchmarkRegistry registry)
        {
            var registrations = Registrations;
            for (var i = 0; i < registrations.Length; i++)
            {
                var entry = registrations[i];
                if (entry.Scope != scope)
                    continue;

                registry.Add(entry.Implementation, entry.Lifetime, entry.Markers);
            }
        }

        public static int Count(ServiceLifetime lifetime)
        {
            var count = 0;
            var registrations = Registrations;
            for (var i = 0; i < registrations.Length; i++)
            {
                if (registrations[i].Lifetime == lifetime)
                    count++;
            }

            return count;
        }

        public static int Count(BenchmarkScopeLevel scope)
        {
            var count = 0;
            var registrations = Registrations;
            for (var i = 0; i < registrations.Length; i++)
            {
                if (registrations[i].Scope == scope)
                    count++;
            }

            return count;
        }
    }
}
