using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

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
            AssetReference serviceScene,
            Func<IScopeBuilder, UniTask> constructCallback,
            bool isMock)
        {
            Parent = parent;
            ServiceScene = serviceScene;
            ConstructCallback = constructCallback;
            IsMock = isMock;
        }

        public ILoadedScope Parent { get; }
        public AssetReference ServiceScene { get; }
        public Func<IScopeBuilder, UniTask> ConstructCallback { get; }
        public bool IsMock { get; }
    }
}