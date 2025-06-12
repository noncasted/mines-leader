using Internal;

namespace Common.Network
{
    public static class NetworkEntitiesExtensions
    {
        public static IEntityBuilder RegisterProperty<T>(this IEntityBuilder builder) where T : new()
        {
            builder.Register<NetworkProperty<T>>()
                .As<INetworkProperty<T>>()
                .As<INetworkProperty>()
                .AsSelfResolvable();

            return builder;
        }
    }
}