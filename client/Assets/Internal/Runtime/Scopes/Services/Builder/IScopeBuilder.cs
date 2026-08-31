namespace Internal
{
    public interface IScopeBuilder : IBuilder
    {
        ISceneLoader SceneLoader { get; }
        IServiceScopeBinder Binder { get; }
        ILifetime ScopeLifetime { get; }
        bool IsMock { get; }
    }
}