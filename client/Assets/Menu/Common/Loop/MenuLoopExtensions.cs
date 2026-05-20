using Internal;
using Menu.Screens.Cards.Preview;

namespace Menu.Common
{
    public static class MenuLoopExtensions
    {
        public static IScopeBuilder AddMenuLoop(this IScopeBuilder builder)
        {
            builder.Register<MenuLoop>()
                   .As<IMenuLoop>();

            builder.Register<MenuCardPreviewPlayer>()
                   .As<IMenuCardPreviewPlayer>();

            return builder;
        }
    }
}