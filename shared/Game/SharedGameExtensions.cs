namespace Shared
{
    public static class SharedGameExtensions
    {
        public static IUnionBuilder<INetworkContext> AddSharedGame(this IUnionBuilder<INetworkContext> builder)
        {
            SharedMoveSnapshot.Register(builder);
            SharedGameAction.Register(builder);
            
            return builder;
        }
    }
}