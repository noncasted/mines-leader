using System.Collections.Generic;

namespace Internal.Tests
{
    // Isolated roots for scope-class emit tests. Complete graphs, no holes.
    // LoadGenerated must emit a class; LoadRuntime must not (pointwise opt-out).
    public static class ScopeCodegenRoots
    {
        [ContainerGraphRoot]
        public static void LoadGenerated(IScopeBuilder builder)
        {
            builder.Register<ScopeCodegenFirstSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenSecondSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenThirdSetup>().As<IScopeSetup>();
            builder.Register<ScopeCodegenSingleton>();
            builder.Register<ScopeCodegenScoped>(VContainer.Lifetime.Scoped);
            builder.Register<ScopeCodegenTransient>(VContainer.Lifetime.Transient);
            builder.Register<ScopeCodegenBronze>().As<IScopeCodegenTier>();
            builder.Register<ScopeCodegenGold>().As<IScopeCodegenTier>();
            builder.Register<ScopeCodegenRow>();
        }

        [ContainerGraphRoot]
        [ContainerRuntimeScope]
        public static void LoadRuntime(IScopeBuilder builder)
        {
            builder.Register<ScopeCodegenRuntimeOnly>();
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

    public sealed class ScopeCodegenRuntimeOnly
    {
    }
}
