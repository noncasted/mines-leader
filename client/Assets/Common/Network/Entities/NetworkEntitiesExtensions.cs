using Internal;

namespace Common.Network
{
    public static class NetworkEntitiesExtensions
    {
        public static IEntityBuilder RegisterProperty<T>(this IEntityBuilder builder) where T : new()
        {
            var id = typeof(T).FullName!.GetHashCode();
            
            builder.Register<NetworkProperty<T>>()
                .WithParameter(id)
                .As<INetworkProperty<T>>()
                .As<INetworkProperty>()
                .AsSelfResolvable();

            return builder;
        }
    }
}