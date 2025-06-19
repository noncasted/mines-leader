using Internal;

namespace Global.Network
{
    public static class GlobalNetworkExtensions
    {
        public static IScopeBuilder AddNetwork(this IScopeBuilder builder)
        {
            builder.Register<NetworkInitializer>()
                .As<IScopeBaseSetup>();
            
            return builder;
        }
    }
}