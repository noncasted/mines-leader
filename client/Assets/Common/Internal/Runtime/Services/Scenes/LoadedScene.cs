using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Internal
{
    public interface ILoadedScene
    {
        Scene Scene { get; }

        UniTask Unload();
    }

    public class LoadedScene : ILoadedScene
    {
        public LoadedScene(SceneInstance instance)
        {
            _instance = instance;
        }

        private readonly SceneInstance _instance;

        public Scene Scene => _instance.Scene;

        public UniTask Unload()
        {
            return Addressables.UnloadSceneAsync(_instance).ToUniTask();
        }
    }

    /// <summary>
    /// Сцена, созданная в рантайме вместо загрузки пустого ассета: контейнеру сервисов
    /// нужен только сам <see cref="Scene"/>, а бандл с нулевым содержимым стоит
    /// открытия файла и пары кадров на async-операции.
    /// </summary>
    public class CreatedScene : ILoadedScene
    {
        public CreatedScene(Scene scene)
        {
            Scene = scene;
        }

        public Scene Scene { get; }

        public UniTask Unload()
        {
            if (Scene.isLoaded != true)
                return UniTask.CompletedTask;

            return SceneManager.UnloadSceneAsync(Scene).ToUniTask();
        }
    }
}
