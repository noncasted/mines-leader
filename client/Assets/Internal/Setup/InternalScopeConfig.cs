using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    public class InternalScopeConfig : EnvAsset, IInternalScopeConfig
    {
        [SerializeField] private AssetsStorage _assetsStorage;

        public IAssetsStorage AssetsStorage => _assetsStorage;
    }
}