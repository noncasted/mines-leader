namespace Internal
{
    public interface IEntityScopeResult
    {
        IContainer Container { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}
