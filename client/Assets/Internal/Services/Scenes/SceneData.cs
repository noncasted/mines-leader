using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Internal
{
    public abstract class SceneData : EnvAsset
    {
        [SerializeField] private AssetReference _value;
        
        public AssetReference Value => _value;
    }
}