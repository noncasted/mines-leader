namespace Shared
{
    public static class SharedGameExtensions
    {
        public static IUnionBuilder<INetworkContext> AddSharedGame(this IUnionBuilder<INetworkContext> builder)
        {
            SharedMoveSnapshot.Register(builder);
            SharedGameAction.Register(builder);
            RematchContexts.Register(builder);
            GameCheatContexts.Register(builder);

            builder.Add<CardsConfigs>();
            builder.Add<MatchActionContexts.PlayerReady>();

            return builder;
        }
    }
}