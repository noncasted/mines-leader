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

            builder.Add<CardConfigOptions>();
            builder.Add<InGameAchievementOptions>();
            builder.Add<MatchMakingOptions>();
            builder.Add<MatchActionContexts.PlayerReady>();
            builder.Add<MatchActionContexts.PlayerLoaded>();
            builder.Add<SharedAgentObservation>();
            builder.Add<SharedAgentObservationRequest>();
            builder.Add<SharedAgentLegalPlaysRequest>();
            builder.Add<SharedAgentLegalPlaysResponse>();

            return builder;
        }
    }
}