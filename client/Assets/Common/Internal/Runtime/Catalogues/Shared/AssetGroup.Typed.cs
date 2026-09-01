using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Internal {
    // Группа, чьё содержимое лежит в одном адресуемом ScriptableObject: хендл держится здесь,
    // генерируемым классам остаётся только разложить поля.
    public abstract class AssetGroup<TAsset> : AssetGroup where TAsset : Object {
        private AsyncOperationHandle<TAsset> _handle;

        protected TAsset Asset { get; private set; }

        protected async UniTask LoadAsset(string address) {
            _handle = Addressables.LoadAssetAsync<TAsset>(address);
            Asset = await _handle.ToUniTask();
        }

        protected void UnloadAsset() {
            Asset = null;
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }
    }
}
