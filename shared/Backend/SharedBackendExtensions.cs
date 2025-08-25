namespace Shared
{
    public static class SharedBackendExtensions
    {
        public static IUnionBuilder<INetworkContext> AddSharedBackend(this IUnionBuilder<INetworkContext> builder)
        {
            builder.Add<SharedBackendProjection>();

            SharedBackendSocketAuth.Register(builder);
            SharedBackendUser.Register(builder);
            SharedMatchmaking.Register(builder);

            return builder;
        }
    }
}