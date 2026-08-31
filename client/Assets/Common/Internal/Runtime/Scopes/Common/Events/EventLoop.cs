using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Internal;

namespace Internal
{
    public interface IEventLoop
    {
        void AddBeforeBuild(Func<UniTask> callback);
        void AddBeforeDispose(Func<UniTask> callback);

        UniTask InvokeBeforeBuild();
        UniTask InvokeBeforeDispose();

        void Bind(IObjectResolver resolver);

        void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T> invoker);
        UniTask RunCustomAsync<T>(IReadOnlyLifetime lifetime, Func<T, UniTask> invoker);
        UniTask RunConstruct(IReadOnlyLifetime lifetime);
        UniTask RunLoaded(IReadOnlyLifetime lifetime);
        UniTask RunDispose();
    }

    public class EventLoop : IEventLoop
    {
        private readonly List<Func<UniTask>> _beforeBuildCallbacks = new();
        private readonly List<Func<UniTask>> _beforeDisposeCallbacks = new();

        private IObjectResolver _resolver;

        public void AddBeforeBuild(Func<UniTask> callback)
        {
            _beforeBuildCallbacks.Add(callback);
        }

        public void AddBeforeDispose(Func<UniTask> callback)
        {
            _beforeDisposeCallbacks.Add(callback);
        }

        public async UniTask InvokeBeforeBuild()
        {
            await InvokeCallbacks(_beforeBuildCallbacks);
            _beforeBuildCallbacks.Clear();
        }

        public async UniTask InvokeBeforeDispose()
        {
            await InvokeCallbacks(_beforeDisposeCallbacks);
            _beforeDisposeCallbacks.Clear();
        }

        public void Bind(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T> invoker)
        {
            Invoke(ResolveList<T>(), invoker);
        }

        public UniTask RunCustomAsync<T>(IReadOnlyLifetime lifetime, Func<T, UniTask> invoker)
        {
            return InvokeAsync(ResolveList<T>(), invoker);
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

        private IReadOnlyList<T> ResolveList<T>()
        {
            return _resolver.Resolve<ContainerLocal<IReadOnlyList<T>>>().Value;
        }

        private void Invoke<T>(IReadOnlyList<T> listeners, Action<T> invoker)
        {
            foreach (var listener in listeners)
                invoker.Invoke(listener);
        }

        private UniTask InvokeAsync<T>(IReadOnlyList<T> listeners, Func<T, UniTask> invoker)
        {
            var count = listeners.Count;
            var tasks = new UniTask[count];

            for (var i = 0; i < count; i++)
                tasks[i] = invoker.Invoke(listeners[i]);

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