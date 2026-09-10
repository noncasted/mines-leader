using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Internal
{
    /// <summary>
    /// Откуда скоуп берёт сцену под свои объекты.
    /// </summary>
    public interface IScopeServiceSceneLoader
    {
        /// <summary>
        /// Имя скоупа в трассе профайлера и в контейнере.
        /// </summary>
        string Name { get; }

        UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader);
    }

    /// <summary>
    /// Сцена сервисов нужна только как контейнер для объектов скоупа, поэтому она создаётся
    /// на ходу: пустая сцена в бандле стоит открытия файла и пары кадров на async-загрузке,
    /// а полезной нагрузки в ней ноль.
    /// </summary>
    public class RuntimeScene : IScopeServiceSceneLoader
    {
        public RuntimeScene(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            var scene = loader.Create(Name);
            return UniTask.FromResult<IServiceScopeBinder>(new ServiceScopeBinder(scene.Scene));
        }
    }

    /// <summary>
    /// Сцена сервисов — ассет с содержимым, которое нужно скоупу.
    /// </summary>
    public class AssetScene : IScopeServiceSceneLoader
    {
        public AssetScene(AssetReference asset)
        {
            _asset = asset;
        }

        private readonly AssetReference _asset;

        public string Name => _asset.AssetGUID;

        public async UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            var scene = await loader.Load(_asset);
            return new ServiceScopeBinder(scene.Scene);
        }
    }

    /// <summary>
    /// Своей сцены у скоупа нет: переносить объекты некуда, биндер падает при использовании.
    /// </summary>
    public class NoScene : IScopeServiceSceneLoader
    {
        public string Name => "Registry";

        public UniTask<IServiceScopeBinder> Load(ServiceScopeSceneLoader loader)
        {
            return UniTask.FromResult<IServiceScopeBinder>(new ExceptionServiceScopeBinder(Name));
        }
    }
}