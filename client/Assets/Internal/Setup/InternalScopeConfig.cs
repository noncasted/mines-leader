using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    public class InternalScopeConfig : EnvAsset, IInternalScopeConfig
    {
        [SerializeField] private InternalScope _scope;
        [SerializeField] private AssetsStorage _assetsStorage;

        public InternalScope Scope => _scope;
        public IAssetsStorage AssetsStorage => _assetsStorage;
    }
}