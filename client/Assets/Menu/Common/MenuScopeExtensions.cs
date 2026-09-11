using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Meta;
using Menu.Decks;
using Menu.Navigation;
using Menu.Play;
using Menu.Profile;
using Menu.Settings;
using Menu.Unlocks;

namespace Menu.Common
{
    public static class MenuScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMenu(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(parent, Construct);

            using var stage = GameProfiler.Scope("Menu");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;
        }

        public static async UniTask<ILoadedScope> LoadMenuMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(parent, Construct)
                          .WithRuntimeScene("Menu_Services")
                          .AsMock();

            using var stage = GameProfiler.Scope("Menu mock");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;
        }

        [ContainerScopeParent(typeof(MetaScopeExtensions), nameof(MetaScopeExtensions.Construct))]
        private static async UniTask Construct(this IScopeBuilder builder)
        {
            using var scope = (GameProfiler.Scope("Registry"));

            await UniTask.WhenAll(
                scope.Measure("Scene: Menu", () => builder.FindOrLoadSceneWithServices(Scenes.Menu.Value)),
                scope.Measure("Scene: Menu_Board", () => builder.FindOrLoadSceneWithServices(Scenes.MenuBoard.Value)),
                scope.Measure("Prefabs: Menu", builder.LoadPrefabGroup(MenuPrefabs.Group)),
                scope.Measure("Prefabs: GamePlay", builder.LoadPrefabGroup(GamePlayPrefabs.Group)),
                scope.Measure("Sprites: GameCells", builder.LoadSpriteGroup(Sprites.GameCells)),
                scope.Measure("Sprites: MenuPlay", builder.LoadSpriteGroup(Sprites.MenuPlay)),
                scope.Measure("Sprites: MenuNavigation", builder.LoadSpriteGroup(Sprites.MenuNavigation)),
                scope.Measure("Sprites: MenuUnlocks", builder.LoadSpriteGroup(Sprites.MenuUnlocks)),
                scope.Measure("Sprites: GameField", builder.LoadSpriteGroup(Sprites.GameField)),
                scope.Measure("Sprites: GameUI", builder.LoadSpriteGroup(Sprites.GameUI)),
                scope.Measure("Sprites: Settings", builder.LoadSpriteGroup(Sprites.Settings)));

            builder.Register<MenuLoop>()
                   .As<IMenuLoop>();

            builder.Register<MenuNavigation>()
                   .As<IMenuNavigation>()
                   .As<IScopeSetup>();

            builder.Register<MenuSettings>()
                   .As<IMenuSettings>();

            builder.Register<MenuDecks>()
                   .As<IMenuDecks>()
                   .As<IMetaSetupCompleted>();

            builder.Injectable<MenuDeckPoolCard>();

            builder.Register<MenuPlay>()
                   .As<IMenuPlay>()
                   .As<IScopeSetup>()
                   .As<IMetaSetupCompleted>();

            builder.Register<MenuProfile>()
                   .As<IMenuProfile>()
                   .As<IScopeSetup>();

            builder.Register<MenuUnlocks>()
                   .As<IMenuUnlocks>()
                   .As<IScopeSetup>();

            builder.Register<MenuCardPreviewPlayer>()
                   .As<IMenuCardPreviewPlayer>();

            builder.Register<MenuCardPreviewCache>()
                   .As<IMenuCardPreviewCache>();

            builder.Register<MenuCardPreviewProjectionHandler>()
                   .As<IScopeSetup>();

            builder.Register<MenuPreviewGameContext>()
                   .As<IGameContext>()
                   .As<IScopeSetup>();

            builder.Register<MenuPreviewGameCamera>()
                   .As<IGameCamera>();

            builder.Register<MenuPreviewVfxFactory>()
                   .As<ICardVfxFactory>()
                   .As<IScopeSetup>();

            builder.Injectable<ZipZapLine>();

            builder.Register<MenuBoardCellsAnimator>()
                   .As<IBoardCellsAnimator>();

            builder.Register<CardActionSyncDispatcher>()
                   .As<ICardActionSyncDispatcher>()
                   .As<IScopeSetup>();

            builder.Register<CardResourceFloatingText>()
                   .As<ICardResourceFloatingText>();

            builder.AddAllCardActionSyncs();

        }
    }
}