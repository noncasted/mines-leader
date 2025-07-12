using Internal;

namespace Global.Backend
{
    public static class GlobalBackendExtensions
    {
        public static IScopeBuilder AddBackend(this IScopeBuilder builder)
        {
            builder.Register<BackendGet>()
                .As<IBackendGet>();

            builder.Register<BackendMedia>()
                .As<IBackendMedia>();

            builder.Register<BackendPost>()
                .As<IBackendPost>();

            builder.Register<BackendClient>()
                .WithAsset<BackendOptions>()
                .As<IBackendClient>();
            

            return builder;
        }
    }
}