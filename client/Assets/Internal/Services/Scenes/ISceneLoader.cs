using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Internal
{
    public interface ISceneLoader
    {
        UniTask<ILoadedScene> Load(AssetReference scene, bool isMain = false);
    }
}