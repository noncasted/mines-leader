using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Tools {
    public abstract class PrefabGroup {
        private int _retainCount;
        private AsyncOperationHandle<PrefabGroupAsset> _handle;

        public bool IsLoaded { get; private set; }

        public async UniTask Retain() {
            _retainCount++;
            if (IsLoaded)
                return;

            await LoadGroup();
            IsLoaded = true;
        }

        public void Release() {
            if (_retainCount == 0) {
                Debug.LogError($"[{GetType().Name}] Release called with retain count 0");
                return;
            }

            _retainCount--;
            if (_retainCount > 0)
                return;

            UnloadGroup();
            IsLoaded = false;
        }

        protected PrefabGroupAsset Asset { get; private set; }

        protected abstract UniTask LoadGroup();

        protected abstract void UnloadGroup();

        protected async UniTask LoadAsset(string address) {
            _handle = Addressables.LoadAssetAsync<PrefabGroupAsset>(address);
            Asset = await _handle.ToUniTask();
        }

        protected void UnloadAsset() {
            Asset = null;
            if (_handle.IsValid())
                Addressables.Release(_handle);
        }

        protected void EnsureLoaded() {
            if (IsLoaded == false)
                throw new InvalidOperationException($"{GetType().Name} is not loaded");
        }
    }
}
