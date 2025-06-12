using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public interface ILoadedScope
    {
        LifetimeScope Container { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Initialize();
        UniTask Dispose();
    }
    
    public static class ServiceLoadResultExtensions
    {
        public static T Get<T>(this ILoadedScope loadResult)
        {
            return loadResult.Container.Container.Resolve<T>();
        }
    }
}