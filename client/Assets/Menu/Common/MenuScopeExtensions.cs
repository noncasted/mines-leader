using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Menu.Decks;

namespace Menu.Common
{
    public static class MenuScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadMenu(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = new ScopeLoadOptions(
                parent,
                "Menu_Services",
                Construct,
                false);

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
            var options = new ScopeLoadOptions(
                parent,
                "Menu_Services",
                Construct,
                true);

            using var stage = GameProfiler.Scope("Menu mock");

            var scope = await loader.Load(options);

            using (GameProfiler.Scope("Loaded"))
                await scope.Initialize();

            return scope;
        }

        private static async UniTask Construct(this IScopeBuilder builder)
        {
            using var construct = (GameProfiler.Scope("Services"));

            await UniTask.WhenAll(
                construct.Measure("Scene: Menu", () => builder.FindOrLoadSceneWithServices(Scenes.Menu.Value)),
                construct.Measure("Scene: MenuBoard",
                    () => builder.FindOrLoadSceneWithServices(Scenes.MenuBoard.Value)),
                // Отрезок на группу открывает сам LoadPrefabGroup/LoadSpriteGroup.
                builder.LoadPrefabGroup(MenuPrefabs.Group),
                builder.LoadPrefabGroup(GamePlayPrefabs.Group),
                builder.LoadSpriteGroup(Sprites.GameCells),
                builder.LoadSpriteGroup(Sprites.MenuPlay),
                builder.LoadSpriteGroup(Sprites.MenuNavigation),
                builder.LoadSpriteGroup(Sprites.MenuUnlocks),
                builder.LoadSpriteGroup(Sprites.GameField),
                builder.LoadSpriteGroup(Sprites.Settings));

            builder.Register<MenuLoop>()
                   .As<IMenuLoop>();

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

            builder.Register<MenuBoardCellsAnimator>()
                   .As<IBoardCellsAnimator>();

            builder.Register<CardActionSyncDispatcher>()
                   .As<ICardActionSyncDispatcher>()
                   .As<IScopeSetup>();

            builder.AddAllCardActionSyncs();

        }
    }
}