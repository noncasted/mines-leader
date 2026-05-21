using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Menu.Decks;

namespace Menu.Common
{
    public static class MenuLoopExtensions
    {
        public static IScopeBuilder AddMenuLoop(this IScopeBuilder builder)
        {
            builder.RegisterAsset<ZipZapOptions>();
            
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

            builder.Register<MenuCardActionSyncRegistry>()
                   .As<IScopeSetup>();

            builder.AddMenuCardActionSyncs();

            return builder;
        }
    }
}