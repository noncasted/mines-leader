using Cysharp.Threading.Tasks;
using Global.Setup;
using Internal;
using Shared;

namespace Meta
{
    public static class MetaScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMeta(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(parent, Construct);

            using var stage = GameProfiler.Scope("Meta");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;
        }

        [ContainerScopeParent(typeof(GlobalScopeExtensions), nameof(GlobalScopeExtensions.Construct))]
        public static UniTask Construct(IScopeBuilder builder)
        {
            // Спрайты меты (CardsIcons, CardBuffs, MenuPlay, Portraits) живут всё время приложения
            // и качаются со старта в StartupAssetsPreload.
            builder.Register<MetaLoop>()
                   .As<IScopeBaseSetupAsync>();

            builder.Register<Decks>()
                   .As<IDecks>()
                   .As<IScopeSetup>();

            builder.Register<MetaBackend>()
                   .WithScopeLifetime()
                   .As<IMetaBackend>();

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
                .RegisterBackendResponse<SharedMatchmaking.LobbyResult>();

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

            return UniTask.CompletedTask;
        }
    }
}