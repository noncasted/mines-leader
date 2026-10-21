using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IEventLoop
    {
        void AddBeforeBuild(Func<UniTask> callback);
        // Runs after the scope is fully torn down (lifetime terminated, scenes unloaded, container disposed):
        // asset groups are released here, so teardown listeners may still read the assets of the scope.
        void AddAfterDispose(Func<UniTask> callback);

        UniTask InvokeBeforeBuild();
        UniTask InvokeAfterDispose();

        void Bind(IContainer container);

        // A custom phase: every listener T gets the lifetime, as in the built-in phases.
        void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T, IReadOnlyLifetime> invoker);
        UniTask RunCustomAsync<T>(IReadOnlyLifetime lifetime, Func<T, IReadOnlyLifetime, UniTask> invoker);
        UniTask RunConstruct(IReadOnlyLifetime lifetime);
        UniTask RunLoaded(IReadOnlyLifetime lifetime);
        UniTask RunDispose();
    }

    public class EventLoop : IEventLoop
    {
        private readonly List<Func<UniTask>> _beforeBuildCallbacks = new();
        private readonly List<Func<UniTask>> _afterDisposeCallbacks = new();

        private IContainer _container;

        public void AddBeforeBuild(Func<UniTask> callback)
        {
            _beforeBuildCallbacks.Add(callback);
        }

        public void AddAfterDispose(Func<UniTask> callback)
        {
            _afterDisposeCallbacks.Add(callback);
        }

        public async UniTask InvokeBeforeBuild()
        {
            await InvokeCallbacks(_beforeBuildCallbacks);
            _beforeBuildCallbacks.Clear();
        }

        public async UniTask InvokeAfterDispose()
        {
            await InvokeCallbacks(_afterDisposeCallbacks);
            _afterDisposeCallbacks.Clear();
        }

        public void Bind(IContainer container)
        {
            _container = container;
        }

        public void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T, IReadOnlyLifetime> invoker)
        {
            Invoke(ResolveList<T>(), l => invoker.Invoke(l, lifetime));
        }

        public UniTask RunCustomAsync<T>(IReadOnlyLifetime lifetime, Func<T, IReadOnlyLifetime, UniTask> invoker)
        {
            return InvokeAsync(ResolveList<T>(), l => invoker.Invoke(l, lifetime));
        }

        public async UniTask RunConstruct(IReadOnlyLifetime lifetime)
        {
            await UniTask.SwitchToMainThread();

            Invoke(ResolveList<IScopeBaseSetup>(), l => {
                l.OnBaseSetup(lifetime);
            });

            await InvokeAsync(ResolveList<IScopeBaseSetupAsync>(), l => {
                return l.OnBaseSetupAsync(lifetime);
            });

            Invoke(ResolveList<IScopeSetup>(), l => {
                l.OnSetup(lifetime);
            });

            await InvokeAsync(ResolveList<IScopeSetupAsync>(), l => {
                return l.OnSetupAsync(lifetime);
            });

            Invoke(ResolveList<IScopeSetupCompletion>(), l => {
                l.OnSetupCompletion(lifetime);
            });

            await InvokeAsync(ResolveList<IScopeSetupCompletionAsync>(), l => {
                return l.OnSetupCompletionAsync(lifetime);
            });
        }

        public async UniTask RunLoaded(IReadOnlyLifetime lifetime)
        {
            Invoke(ResolveList<IScopeLoaded>(), l => {
                l.OnLoaded(lifetime);
            });

            await InvokeAsync(ResolveList<IScopeLoadedAsync>(), l => {
                return l.OnLoadedAsync(lifetime);
            });
        }

        public async UniTask RunDispose()
        {
            Invoke(ResolveList<IScopeDispose>(), l => {
                l.OnDispose();
            });

            await InvokeAsync(ResolveList<IScopeDisposeAsync>(), l => {
                return l.OnDisposeAsync();
            });
        }

        /// <summary>
        /// Резолв списка — это ещё и создание всех сервисов этапа, поэтому он меряется
        /// отдельно от их собственных колбэков: иначе стоимость конструирования растворяется.
        /// </summary>
        private IReadOnlyList<T> ResolveList<T>()
        {
            using (GameProfiler.Scope($"Resolve: {typeof(T).Name}"))
                return _container.ResolveAll<T>();
        }

        /// <summary>
        /// Синхронные слушатели идут строго по очереди, поэтому каждый ложится в трассу
        /// отдельным этапом и его собственные замеры вкладываются внутрь. Асинхронные
        /// стартуют пачкой через WhenAll: их вложенность по стеку не строится, и те,
        /// у кого есть что замерять, открывают свой отрезок сами (см. <see cref="GameProfiler.Concurrent"/>).
        /// </summary>
        private void Invoke<T>(IReadOnlyList<T> listeners, Action<T> invoker)
        {
            if (GameProfiler.IsRunning == false)
            {
                foreach (var listener in listeners)
                    invoker.Invoke(listener);

                return;
            }

            foreach (var listener in listeners)
            {
                using (GameProfiler.Scope(listener.GetType().Name))
                    invoker.Invoke(listener);
            }
        }

        /// <summary>
        /// Асинхронные слушатели стартуют пачкой, поэтому отрезок каждого берётся параллельным
        /// и на стек не встаёт. На время синхронного пролога он всё же делается текущим: до
        /// первого await слушатель успевает забрать его через <see cref="GameProfiler.CurrentScope"/>
        /// и повесить на него свои шаги.
        /// </summary>
        private UniTask InvokeAsync<T>(IReadOnlyList<T> listeners, Func<T, UniTask> invoker)
        {
            var count = listeners.Count;
            var tasks = new UniTask[count];

            if (GameProfiler.IsRunning == false)
            {
                for (var i = 0; i < count; i++)
                    tasks[i] = invoker.Invoke(listeners[i]);

                return UniTask.WhenAll(tasks);
            }

            for (var i = 0; i < count; i++)
            {
                var listener = listeners[i];
                var scope = GameProfiler.Concurrent(listener.GetType().Name);

                UniTask task;

                using (GameProfiler.Ambient(scope))
                    task = invoker.Invoke(listener);

                tasks[i] = scope.Track(task);
            }

            return UniTask.WhenAll(tasks);
        }

        private static UniTask InvokeCallbacks(List<Func<UniTask>> callbacks)
        {
            if (callbacks.Count == 0)
                return UniTask.CompletedTask;

            var tasks = new UniTask[callbacks.Count];

            for (var i = 0; i < callbacks.Count; i++)
                tasks[i] = callbacks[i].Invoke();

            return UniTask.WhenAll(tasks);
        }
    }
}