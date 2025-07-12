namespace Shared
{
    public static class SharedSessionExtensions
    {
        public static IUnionBuilder<INetworkContext> AddSharedSession(this IUnionBuilder<INetworkContext> builder)
        {
            SharedSessionAuth.Register(builder);
            SharedSessionEntity.Register(builder);
            SharedSessionObject.Register(builder);
            SharedSessionPlayer.Register(builder);
            SharedSessionService.Register(builder);

            return builder;
        }
    }
}