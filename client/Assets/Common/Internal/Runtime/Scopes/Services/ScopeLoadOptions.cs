using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Internal
{
    public class ScopeLoadOptions
    {
        public ScopeLoadOptions(ILoadedScope parent, Func<IScopeBuilder, UniTask> constructCallback)
            : this(parent, constructCallback, constructCallback.Method)
        {
        }

        private ScopeLoadOptions(
            ILoadedScope parent,
            Func<IScopeBuilder, UniTask> constructCallback,
            MethodInfo root)
        {
            Parent = parent;
            ConstructCallback = constructCallback;
            RootId = GeneratedScopes.RootId(root);
        }

        /// <summary>
        /// Корень с аргументом. Корень передаётся группой методов, а не лямбдой: по нему
        /// выбирается сгенерированный класс скоупа.
        /// </summary>
        public static ScopeLoadOptions Create<TArg>(
            ILoadedScope parent,
            Func<IScopeBuilder, TArg, UniTask> construct,
            TArg arg)
        {
            return new ScopeLoadOptions(parent, builder => construct(builder, arg), construct.Method);
        }

        public ILoadedScope Parent { get; }
        public Func<IScopeBuilder, UniTask> ConstructCallback { get; }
        public string RootId { get; }
        public IScopeServiceSceneLoader SceneLoader { get; private set; } = new NoScene();
        public bool IsMock { get; private set; }

        /// <summary>
        /// Скоуп — часть мока.
        /// </summary>
        public ScopeLoadOptions AsMock()
        {
            IsMock = true;
            return this;
        }

        public ScopeLoadOptions WithRuntimeScene(string name)
        {
            SceneLoader = new RuntimeScene(name);
            return this;
        }

        public ScopeLoadOptions WithAssetScene(AssetReference asset)
        {
            SceneLoader = new AssetScene(asset);
            return this;
        }
    }
}