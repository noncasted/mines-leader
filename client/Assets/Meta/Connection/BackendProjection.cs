using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IBackendProjection
    {
        /// <summary>The value has arrived at least once.</summary>
        IViewableProperty<bool> IsInitialized { get; }

        Type GetValueType();
        void OnReceived(INetworkContext context);
    }

    /// <summary>
    /// A projection the server sends right on connect: without it the meta is not ready
    /// (see <see cref="MetaState"/>).
    /// </summary>
    public interface IInitialBackendProjection : IBackendProjection
    {
    }

    public interface IBackendProjection<T> : IViewableProperty<T> where T : class, INetworkContext
    {
    }

    public class BackendProjection<T> : IInitialBackendProjection, IBackendProjection<T>
        where T : class, INetworkContext
    {
        private readonly LifetimedValue<T> _lifetimedValue = new(null);
        private readonly ViewableProperty<bool> _isInitialized = new(false);

        public T Value => _lifetimedValue.Value;
        public IReadOnlyLifetime ValueLifetime => _lifetimedValue.ValueLifetime;
        public IViewableProperty<bool> IsInitialized => _isInitialized;

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _lifetimedValue.Advise(lifetime, handler);
        }

        public void Dispose()
        {
            _lifetimedValue.Dispose();
            _isInitialized.Dispose();
        }

        public Type GetValueType()
        {
            return typeof(T);
        }

        public void OnReceived(INetworkContext context)
        {
            // The flag is set after the value: whoever waited for readiness already sees the data,
            // and the subscribers of the projection itself have had time to process it.
            _lifetimedValue.Set((T)context);
            _isInitialized.Set(true);
        }
    }

    public static class BackendProjectionExtensions
    {
        public static UniTask<T> WaitOnce<T>(
            this IBackendProjection<T> projection,
            IReadOnlyLifetime lifetime) where T : class, INetworkContext
        {
            var listenLifetime = lifetime.Child();
            var completion = new UniTaskCompletionSource<T>();
            listenLifetime.Listen(() => completion.TrySetCanceled());
            projection.Advise(listenLifetime, Listener);
            return completion.Task;

            void Listener(T update)
            {
                completion.TrySetResult(update);
                listenLifetime.Terminate();
            }
        }

        public static IScopeBuilder RegisterBackendProjection<T>(this IScopeBuilder builder)
            where T : class, INetworkContext
        {
            builder.Register<BackendProjection<T>>()
                   .As<IBackendProjection<T>>()
                   .As<IBackendProjection>()
                   .As<IInitialBackendProjection>();

            return builder;
        }

        /// <summary>
        /// A reply to a request (match search, lobby) that travels over the same projection channel. It does not
        /// arrive on connect, so the meta readiness does not wait for it.
        /// </summary>
        public static IScopeBuilder RegisterBackendResponse<T>(this IScopeBuilder builder)
            where T : class, INetworkContext
        {
            builder.Register<BackendProjection<T>>()
                   .As<IBackendProjection<T>>()
                   .As<IBackendProjection>();

            return builder;
        }
    }
}