using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public static class MetaScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMeta(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                "Meta_Services",
                Construct,
                false);

            using var stage = GameProfiler.Scope("Meta");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                builder.LoadSpriteGroup(Sprites.CardsIcons);
                builder.LoadSpriteGroup(Sprites.CardBuffs);
                builder.LoadSpriteGroup(Sprites.MenuPlay);

                builder.Register<MetaLoop>()
                       .As<IScopeBaseSetupAsync>();

                builder.Register<Decks>()
                       .WithScopeLifetime()
                       .As<IDecks>()
                       .As<IScopeSetup>();

                builder.Register<MetaBackend>()
                       .WithScopeLifetime()
                       .As<IMetaBackend>();

                builder.Register<Matchmaking>()
                       .As<IMatchmaking>();

                builder.Register<Authentication>()
                       .As<IAuthentication>();

                builder.Register<CardsRegistry>()
                       .As<ICardsRegistry>();

                builder.Register<GameModesRegistry>()
                       .As<IGameModesRegistry>();

                builder.Register<ModifiersRegistry>()
                       .As<IModifiersRegistry>();

                builder.Register<CardDescriptionProvider>()
                       .As<ICardDescriptionProvider>();

                builder.AddNetworkConnection();

                builder.RegisterCommand<BackendProjectionHub>();

                builder.RegisterCommand<ConnectionCompletedCommand>()
                       .As<IMetaConnectionAwaiter>();

                builder
                    .RegisterBackendProjection<SharedBackendUser.ProfileProjection>()
                    .RegisterBackendProjection<SharedBackendUser.UserStatsProjection>()
                    .RegisterBackendProjection<SharedBackendUser.InGameAchievementsProjection>()
                    .RegisterBackendProjection<SharedBackendUser.RatingProjection>()
                    .RegisterBackendProjection<SharedBackendUser.DeckProjection>()
                    .RegisterBackendProjection<SharedBackendUser.CardsProjection>()
                    .RegisterBackendProjection<SharedMatchmaking.MatchResult>()
                    .RegisterBackendProjection<SharedMatchmaking.LobbyResult>()
                    .RegisterBackendProjection<InitialCardPreviews>();

                builder.Register<CardConfigs>()
                       .As<IBackendProjection<CardConfigOptions>>()
                       .As<IBackendProjection>()
                       .As<ICardConfigs>();

                builder.Register<InGameAchievementConfigs>()
                       .As<IBackendProjection<InGameAchievementOptions>>()
                       .As<IBackendProjection>()
                       .As<IInGameAchievementConfigs>();

                builder.Register<MatchMakingConfigs>()
                       .As<IBackendProjection<MatchMakingOptions>>()
                       .As<IBackendProjection>()
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
}