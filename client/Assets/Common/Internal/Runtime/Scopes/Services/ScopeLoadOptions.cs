using System;
using System.Reflection;
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
            : this(parent, serviceScene, null, constructCallback, constructCallback.Method, isMock)
        {
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
            : this(parent, null, serviceSceneName, constructCallback, constructCallback.Method, isMock)
        {
        }

        private ScopeLoadOptions(
            ILoadedScope parent,
            AssetReference serviceScene,
            string serviceSceneName,
            Func<IScopeBuilder, UniTask> constructCallback,
            MethodInfo root,
            bool isMock)
        {
            Parent = parent;
            ServiceScene = serviceScene;
            ServiceSceneName = serviceSceneName;
            ConstructCallback = constructCallback;
            RootId = GeneratedScopes.RootId(root);
            IsMock = isMock;
        }

        /// <summary>
        /// Корень с аргументом. Корень передаётся группой методов, а не лямбдой: по нему
        /// выбирается сгенерированный класс скоупа.
        /// </summary>
        public static ScopeLoadOptions Create<TArg>(
            ILoadedScope parent,
            string serviceSceneName,
            Func<IScopeBuilder, TArg, UniTask> construct,
            TArg arg,
            bool isMock)
        {
            return new ScopeLoadOptions(
                parent,
                null,
                serviceSceneName,
                builder => construct(builder, arg),
                construct.Method,
                isMock);
        }

        public ILoadedScope Parent { get; }
        public AssetReference ServiceScene { get; }
        public string ServiceSceneName { get; }
        public Func<IScopeBuilder, UniTask> ConstructCallback { get; }
        public string RootId { get; }
        public bool IsMock { get; }
    }
}
