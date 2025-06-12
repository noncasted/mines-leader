using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public class ScopeLoadResult : ILoadedScope
    {
        public ScopeLoadResult(
            LifetimeScope scope,
            ILifetime lifetime,
            IEventLoop eventLoop,
            IReadOnlyList<ILoadedScene> scenes)
        {
            _scopeLifetime = lifetime;
            _scenes = scenes;
            Container = scope;
            Lifetime = lifetime;
            EventLoop = eventLoop;
        }

        private readonly ILifetime _scopeLifetime;
        private readonly IReadOnlyList<ILoadedScene> _scenes;

        public LifetimeScope Container { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public IEventLoop EventLoop { get; }

        public UniTask Initialize()
        {
            return EventLoop.RunLoaded(Lifetime);
        }

        public async UniTask Dispose()
        {
            await EventLoop.RunDispose();
            _scopeLifetime.Terminate();
            await _scenes.InvokeAsync(scene => scene.Unload());
            Container.Dispose();
        }
    }
}