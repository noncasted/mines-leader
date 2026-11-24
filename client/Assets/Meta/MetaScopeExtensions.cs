using Cysharp.Threading.Tasks;
using Global.MatchMetrics;
using Global.Setup;
using Global.Telemetry;
using Internal;
using Shared;

namespace Meta
{
    public static class MetaScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMeta(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(parent, Construct);

            using var stage = GameProfiler.Stage("Meta");

            return await loader.Load(options);
        }

        [ContainerScopeParent(typeof(GlobalScopeExtensions), nameof(GlobalScopeExtensions.Construct))]
        public static UniTask Construct(IScopeBuilder builder)
        {
            // Спрайты меты (CardsIcons, CardBuffs, MenuPlay, Portraits) живут всё время приложения
            // и качаются со старта в StartupAssetsPreload.
            builder.Register<MetaLoop>()
                   .As<IScopeSetup>()
                   .As<IMetaConnection>();

            builder.Register<Decks>()
                   .As<IDecks>()
                   .As<IScopeSetup>();

            builder.Register<MetaBackend>()
                   .WithScopeLifetime()
                   .As<IMetaBackend>()
                   .As<ITelemetrySocket>()
                   .As<IMatchMetricsSocket>();

            builder.Register<Matchmaking>()
                   .As<IMatchmaking>();

            builder.Register<Authentication>()
                   .As<IAuthentication>();

            builder.Register<MetaState>()
                   .As<IMetaState>()
                   .As<IScopeSetup>();

            builder.Register<CardsRegistry>()
                   .As<ICardsRegistry>()
                   .As<IMetaRegistry>();

            builder.Register<GameModesRegistry>()
                   .As<IGameModesRegistry>()
                   .As<IMetaRegistry>();

            builder.Register<ModifiersRegistry>()
                   .As<IModifiersRegistry>()
                   .As<IMetaRegistry>();

            builder.Register<CardDescriptionProvider>()
                   .As<ICardDescriptionProvider>();

            builder.AddNetworkConnection();

            builder.RegisterCommand<BackendProjectionHub>();

            builder
                .RegisterBackendProjection<SharedBackendUser.ProfileProjection>()
                .RegisterBackendProjection<SharedBackendUser.UserStatsProjection>()
                .RegisterBackendProjection<SharedBackendUser.InGameAchievementsProjection>()
                .RegisterBackendProjection<SharedBackendUser.RatingProjection>()
                .RegisterBackendProjection<SharedBackendUser.DeckProjection>()
                .RegisterBackendProjection<SharedBackendUser.CardsProjection>()
                .RegisterBackendProjection<InitialCardPreviews>()
                .RegisterBackendResponse<SharedMatchmaking.MatchResult>()
                // UserMatchHistory pushes the overview of every finished match; the menu has no history view yet.
                .RegisterBackendResponse<SharedBackendUser.Match>();

            builder.Register<CardConfigs>()
                   .As<IBackendProjection<CardConfigOptions>>()
                   .As<IBackendProjection>()
                   .As<IInitialBackendProjection>()
                   .As<ICardConfigs>();

            builder.Register<InGameAchievementConfigs>()
                   .As<IBackendProjection<InGameAchievementOptions>>()
                   .As<IBackendProjection>()
                   .As<IInitialBackendProjection>()
                   .As<IInGameAchievementConfigs>();

            builder.Register<MatchMakingConfigs>()
                   .As<IBackendProjection<MatchMakingOptions>>()
                   .As<IBackendProjection>()
                   .As<IInitialBackendProjection>()
                   .As<IMatchMakingConfigs>();

            builder.Register<AchievementsService>()
                   .As<IAchievements>()
                   .As<IScopeSetup>();

            builder.Register<AchievementRewards>()
                   .As<IAchievementRewards>();

            builder.Register<Profile>()
                   .As<IProfile>()
                   .As<IScopeSetup>();

            // The telemetry and the match metrics go over the meta socket: the scopes below (GameLoop, the match) open
            // the sessions and the streams. The framerate and resolution modules are not in this game: no collectors of theirs.
            builder.AddSocketClientTelemetry(new ClientTelemetryOptions());
            builder.AddStandardTelemetryCollectors();
            builder.AddSocketMatchMetrics();

            return UniTask.CompletedTask;
        }
    }
}