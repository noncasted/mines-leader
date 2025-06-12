using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface ISceneLoader
    {
        UniTask<ILoadedScene> Load(SceneData data, bool isMain = false);
    }
}