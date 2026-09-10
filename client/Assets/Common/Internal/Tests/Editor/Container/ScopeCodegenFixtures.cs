using System.Collections.Generic;

namespace Internal.Tests
{
    // Isolated root for scope-class emit tests. Complete graph, no holes.
    public static class ScopeCodegenRoots
    {
        [ContainerGraphRoot]
        public static void LoadGenerated(IScopeBuilder builder)
        {
            builder.Register<ScopeCodegenFirstSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenSecondSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenThirdSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenSingleton>();
            builder.Register<ScopeCodegenScoped>(ServiceLifetime.Scoped);
            builder.Register<ScopeCodegenTransient>(ServiceLifetime.Transient);
            builder.Register<ScopeCodegenBronze>().As<IScopeCodegenTier>();
            builder.Register<ScopeCodegenGold>().As<IScopeCodegenTier>();
            builder.Register<ScopeCodegenRow>();
        }
    }

    public interface IScopeCodegenTier
    {
    }

    public sealed class ScopeCodegenFirstSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }
    }

    public sealed class ScopeCodegenSecondSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }
    }

    public sealed class ScopeCodegenThirdSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }
    }

    public sealed class ScopeCodegenSingleton
    {
        public static int Instances;

        public ScopeCodegenSingleton()
        {
            Instances++;
        }
    }

    public sealed class ScopeCodegenScoped
    {
        public static int Instances;

        public ScopeCodegenScoped()
        {
            Instances++;
        }
    }

    public sealed class ScopeCodegenTransient
    {
        public static int Instances;

        public ScopeCodegenTransient(ScopeCodegenSingleton singleton)
        {
            Instances++;
            Singleton = singleton;
        }

        public ScopeCodegenSingleton Singleton { get; }
    }

    public sealed class ScopeCodegenBronze : IScopeCodegenTier
    {
    }

    public sealed class ScopeCodegenGold : IScopeCodegenTier
    {
    }

    public sealed class ScopeCodegenRow
    {
        public ScopeCodegenRow(IReadOnlyList<IScopeCodegenTier> tiers)
        {
            Tiers = tiers;
        }

        public IReadOnlyList<IScopeCodegenTier> Tiers { get; }
    }
}
