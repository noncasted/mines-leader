using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Сцены скоупа: всё, что загружено или создано здесь, выгружается вместе со скоупом.
    public class ServiceScopeSceneLoader
    {
        private readonly List<ILoadedScene> _results = new();

        public IReadOnlyList<ILoadedScene> Results => _results;

        public async UniTask<ILoadedScene> Load(AssetReference scene, bool isMain = false)
        {
            var handle = await Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive).ToUniTask();

            if (isMain == true)
                SceneManager.SetActiveScene(handle.Scene);

            var result = new LoadedScene(handle);
            _results.Add(result);

            return result;
        }

        /// <summary>
        /// Пустой контейнер под объекты скоупа: сцену-ассет для него грузить незачем,
        /// выгружается она вместе с остальными сценами скоупа.
        /// </summary>
        public ILoadedScene Create(string name)
        {
            var scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.None));
            var result = new CreatedScene(scene);

            _results.Add(result);

            return result;
        }
    }
}