using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Internal
{
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
            ServiceSceneName = null;
            ConstructCallback = constructCallback;
            IsMock = isMock;
        }

        /// <summary>
        /// Вариант для скоупов, у которых сцена сервисов пустая: вместо загрузки ассета
        /// сцена с этим именем создаётся в рантайме.
        /// </summary>
        public ScopeLoadOptions(
            ILoadedScope parent,
            string serviceSceneName,
            Func<IScopeBuilder, UniTask> constructCallback,
            bool isMock)
        {
            Parent = parent;
            ServiceScene = null;
            ServiceSceneName = serviceSceneName;
            ConstructCallback = constructCallback;
            IsMock = isMock;
        }

        public ILoadedScope Parent { get; }
        public AssetReference ServiceScene { get; }
        public string ServiceSceneName { get; }
        public Func<IScopeBuilder, UniTask> ConstructCallback { get; }
        public bool IsMock { get; }
    }
}
