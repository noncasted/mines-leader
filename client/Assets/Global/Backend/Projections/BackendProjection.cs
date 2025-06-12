using System;
using Internal;
using Shared;

namespace Global.Backend
{
    public interface IBackendProjection
    {
        Type GetValueType();
        void OnReceived(INetworkContext context);
    }

    public interface IBackendProjection<T> : IViewableProperty<T> where T : class, INetworkContext
    {
    }

    public class BackendProjection<T> : IBackendProjection, IBackendProjection<T> where T : class, INetworkContext
    {
        private readonly LifetimedValue<T> _lifetimedValue = new(null);

        public T Value => _lifetimedValue.Value;
        public IReadOnlyLifetime ValueLifetime => _lifetimedValue.ValueLifetime;

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _lifetimedValue.Advise(lifetime, handler);
        }

        public void Dispose()
        {
            _lifetimedValue.Dispose();
        }

        public Type GetValueType()
        {
            return typeof(T);
        }

        public void OnReceived(INetworkContext context)
        {
            _lifetimedValue.Set((T)context);
        }
    }

    public static class BackendProjectionExtensions
    {
        public static void Listen<T>(
            this IBackendProjection<T> projection,
            IReadOnlyLifetime lifetime,
            Action<T> listener) where T : class, INetworkContext
        {
            projection.Advise(lifetime, listener.Invoke);

            if (projection.Value != null)
                listener.Invoke(projection.Value);
        }

        public static IScopeBuilder RegisterBackendProjection<T>(this IScopeBuilder builder)
            where T : class, INetworkContext
        {
            builder.Register<BackendProjection<T>>()
                .As<IBackendProjection<T>>()
                .As<IBackendProjection>();

            return builder;
        }
    }
}