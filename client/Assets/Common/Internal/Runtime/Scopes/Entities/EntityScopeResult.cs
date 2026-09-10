namespace Internal
{
    public class EntityScopeResult : IEntityScopeResult
    {
        public EntityScopeResult(IContainer container, IReadOnlyLifetime lifetime)
        {
            Container = container;
            Lifetime = lifetime;
        }

        public IContainer Container { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}
