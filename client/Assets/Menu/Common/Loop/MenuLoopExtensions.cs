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
            builder.Register<MenuLoop>()
                   .As<IMenuLoop>();

            builder.Register<MenuCardPreviewPlayer>()
                   .As<IMenuCardPreviewPlayer>()
                   .As<IScopeSetup>();
            builder.Register<MenuCardPreviewCache>()
                   .As<IMenuCardPreviewCache>();
            builder.Register<MenuCardPreviewProjectionHandler>()
                   .As<IScopeSetup>();

            builder.Register<MenuPreviewGameContext>()
                   .As<IGameContext>()
                   .As<IScopeSetup>();
            builder.Register<MenuPreviewCardRandomAnimator>()
                   .As<ICardRandomAnimator>();
            builder.Register<MenuPreviewGameCamera>()
                   .As<IGameCamera>();
            builder.Register<MenuPreviewVfxFactory>()
                   .As<IScopeSetup>();
            builder.Register<MenuCardActionSyncRegistry>()
                   .As<IScopeSetup>();

            builder.AddMenuCardActionSyncs();

            return builder;
        }
    }
}
