using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface ILoadedScope
    {
        IContainer Container { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Initialize();
        UniTask Dispose();
    }

    public static class ServiceLoadResultExtensions
    {
        public static T Resolve<T>(this ILoadedScope loadResult)
        {
            return loadResult.Container.Resolve<T>();
        }
    }
}