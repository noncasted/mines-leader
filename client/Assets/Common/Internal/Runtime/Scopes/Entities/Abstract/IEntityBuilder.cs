using VContainer.Unity;

namespace Internal
{
    public interface IEntityBuilder : IBuilder
    {
        LifetimeScope Scope { get; }
        ILifetime ScopeLifetime { get; }
        IScopeEntityView View { get; }
    }
}