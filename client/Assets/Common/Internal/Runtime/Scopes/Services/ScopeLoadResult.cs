using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public class ScopeLoadResult : ILoadedScope
    {
        public ScopeLoadResult(
            IContainer container,
            ILifetime lifetime,
            IEventLoop eventLoop,
            IReadOnlyList<ILoadedScene> scenes)
        {
            _scopeLifetime = lifetime;
            _scenes = scenes;
            Container = container;
            Lifetime = lifetime;
            EventLoop = eventLoop;
        }

        private readonly ILifetime _scopeLifetime;
        private readonly IReadOnlyList<ILoadedScene> _scenes;

        public IContainer Container { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public IEventLoop EventLoop { get; }

        public UniTask Initialize()
        {
            return EventLoop.RunLoaded(Lifetime);
        }

        public async UniTask Dispose()
        {
            await EventLoop.RunDispose();
            await EventLoop.InvokeBeforeDispose();
            _scopeLifetime.Terminate();
            await _scenes.InvokeAsync(scene => scene.Unload());
            Container.Dispose();
        }
    }
}