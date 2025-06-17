using Common.Network;
using Internal;

namespace Menu
{
    public static class MenuSocialExtensions
    {
        public static IScopeBuilder AddMenuSocial(this IScopeBuilder builder)
        {
            builder.Register<MenuPlayersCollection>()
                .As<IMenuPlayersCollection>();

            builder.Register<MenuSocialLoop>()
                .As<IMenuSocialLoop>();

            builder.AddNetworkService<MenuChat>("menu-chat")
                .Registration.As<IMenuChat>();

            return builder;
        }
    }
}