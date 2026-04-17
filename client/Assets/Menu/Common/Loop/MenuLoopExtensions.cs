using GamePlay.Cards;
using GamePlay.Loop;
using GamePlay.Services;
using Internal;
using Menu.Screens.Cards.Preview;
using Menu.Screens.Cards.Preview.Sync;
using Meta;

namespace Menu.Common
{
    public static class MenuLoopExtensions
    {
        public static IScopeBuilder AddMenuLoop(this IScopeBuilder builder)
        {
            builder.Register<MenuLoop>()
                   .As<IMenuLoop>();

            builder.Register<MenuCardPreviewCache>()
                   .As<IMenuCardPreviewCache>();

            builder.Register<MenuCardPreviewPlayer>()
                   .As<IMenuCardPreviewPlayer>()
                   .As<IScopeSetup>();

            builder.Register<MenuCardPreviewProjectionHandler>()
                   .As<IScopeSetup>();

            // --- Card preview sync pipeline: reuse all gameplay ICardActionSync<> implementations.
            // Every Snapshot class reads its board through IGameContext.GetPlayer(targetId).Board,
            // so we wire a single-player IGameContext whose Board points at Menu_Board.
            builder.Register<MenuPreviewGameContext>()
                   .As<IGameContext>()
                   .As<IScopeSetup>();

            builder.Register<MenuPreviewGameCamera>()
                   .As<IGameCamera>();

            builder.Register<MenuPreviewVfxFactory>()
                   .AsSelfResolvable()
                   .As<ICardVfxFactory>()
                   .As<IScopeSetup>();

            builder.Register<MenuCardActionSyncRegistry>()
                   .AsSelfResolvable()
                   .As<IScopeSetup>();

            // Register every card's ICardActionSync<T>. The registry builds a payload-type map at
            // OnSetup, so dispatch from MenuCardPreviewPlayer picks the right sync automatically.
            // _Max variants share the Snapshot class with their _Normal counterpart — registering
            // both would conflict in VContainer (same Resolver<TImpl, TData>), so we skip them.
            var cards = new CardsRegistry();

            foreach (var definition in cards.Entries.Values)
            {
                if (definition.Type.ToString().EndsWith("_Max") == true)
                    continue;

                builder.AddCardActionSync(definition);
            }

            return builder;
        }
    }
}
