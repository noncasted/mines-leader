using Cysharp.Threading.Tasks;
using Internal;

namespace Flow.Startup
{
    public class InternalLoadedScope : ILoadedScope
    {
        public InternalLoadedScope(IContainer container, ILifetime lifetime)
        {
            _lifetime = lifetime;
            Container = container;
        }

        private readonly ILifetime _lifetime;

        public IContainer Container { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public UniTask Initialize()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Dispose()
        {
            _lifetime.Terminate();
            Container.Dispose();

            return UniTask.CompletedTask;
        }
    }
}
