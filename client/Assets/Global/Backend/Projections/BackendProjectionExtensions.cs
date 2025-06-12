using Internal;
using Shared;

namespace Global.Backend
{
    public static class BackendProjectionExtensions
    {
        public static IRegistration AsBackendProjection<T>(this IRegistration registration)
            where T : INetworkContext
        {
            registration.Builder.Register<BackendProjectionResolver<T>>()
                .AsSelfResolvable();

            registration.As<IBackendProjection<T>>();

            return registration;
        }
    }

    public class BackendProjectionResolver<T> where T : INetworkContext
    {
        public BackendProjectionResolver(IBackendProjectionHub hub, IBackendProjection<T> projection)
        {
            hub.AddListener(projection);
        }
    }
}