using VContainer;

namespace Internal
{
    public static class InternalScenesExtensions
    {
        public static IInternalScopeBuilder AddScenes(this IInternalScopeBuilder builder)
        {
            builder.Container.Register<SceneLoader>(VContainer.Lifetime.Singleton)
                .As<ISceneLoader>();

            return builder;
        }
    }
}