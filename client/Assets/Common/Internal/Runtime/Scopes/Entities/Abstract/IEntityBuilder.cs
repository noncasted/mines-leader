namespace Internal
{
    public interface IEntityBuilder : IBuilder
    {
        ILifetime ScopeLifetime { get; }
        IScopeEntityView View { get; }
    }
}
