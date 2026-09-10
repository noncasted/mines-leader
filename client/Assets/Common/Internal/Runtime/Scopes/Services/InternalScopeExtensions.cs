namespace Internal
{
    public static class InternalScopeExtensions
    {
        // Корень всей цепочки скоупов. Лежит в Internal, чтобы Global мог объявить его родителем.
        public static void Construct(IBuilder builder)
        {
            builder.Register<SceneLoader>()
                   .As<ISceneLoader>();

            builder.Register<ServiceScopeLoader>()
                   .As<IServiceScopeLoader>();

            builder.Register<EntityScopeLoader>()
                   .As<IEntityScopeLoader>();

            InternalAssets.OptionsContainer.Register(builder);
        }
    }
}