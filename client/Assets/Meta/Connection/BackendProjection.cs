using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IBackendProjection
    {
        /// <summary>Значение приехало хотя бы раз.</summary>
        IViewableProperty<bool> IsInitialized { get; }

        Type GetValueType();
        void OnReceived(INetworkContext context);
    }

    /// <summary>
    /// Проекция, которую сервер присылает сразу при подключении: без неё мета не готова
    /// (см. <see cref="MetaState"/>).
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
            // Флаг ставится после значения: кто дождался готовности, уже видит данные,
            // а подписчики самой проекции успели их разобрать.
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
        /// Ответ на запрос (поиск матча, лобби), который едет тем же каналом проекций. При подключении
        /// он не приезжает, поэтому готовность меты его не ждёт.
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