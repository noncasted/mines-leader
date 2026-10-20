using Shared;

namespace Internal
{
    public static class NetworkEntitiesExtensions
    {
        public static IEntityBuilder RegisterProperty<T>(this IEntityBuilder builder, int id = 0) where T : new()
        {
            // The id is on the wire: other clients and the backend must get the same value for the type.
            if (id == 0)
                id = StableHash.Get(typeof(T).FullName!);

            builder.Register<NetworkProperty<T>>()
                   .WithParameter(id)
                   .As<INetworkProperty<T>>()
                   .As<INetworkProperty>()
                   .AsSelfResolvable();

            return builder;
        }
    }
}