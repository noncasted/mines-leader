using Internal;

namespace Common.Network
{
    public static class NetworkEntitiesExtensions
    {
        private static int _incrementalId = 0;
        
        public static IEntityBuilder RegisterProperty<T>(this IEntityBuilder builder, int id = -1) where T : new()
        {
            if (id == -1)
            {
                _incrementalId++;
                id = _incrementalId;
            }
            
            builder.Register<NetworkProperty<T>>()
                .WithParameter(id)
                .As<INetworkProperty<T>>()
                .As<INetworkProperty>()
                .AsSelfResolvable();

            return builder;
        }
    }
}