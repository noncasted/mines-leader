using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IServiceScopeLoader
    {
        IAssetEnvironment Assets { get; }

        UniTask<ILoadedScope> Load(ScopeLoadOptions options);
    }

    public class ScopeLoadOptions
    {
        public ScopeLoadOptions(
            ILoadedScope parent,
            SceneData serviceScene,
            Func<IScopeBuilder, UniTask> constructCallback,
            bool isMock)
        {
            Parent = parent;
            ServiceScene = serviceScene;
            ConstructCallback = constructCallback;
            IsMock = isMock;
        }

        public ILoadedScope Parent { get; }
        public SceneData ServiceScene { get; }
        public Func<IScopeBuilder, UniTask> ConstructCallback { get; }
        public bool IsMock { get; }
    }
}