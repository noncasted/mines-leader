using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tools {
    public abstract class SpriteGroup {
        private int _retainCount;

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

        protected abstract UniTask LoadGroup();

        protected abstract void UnloadGroup();

        protected void EnsureLoaded() {
            if (IsLoaded == false)
                throw new InvalidOperationException($"{GetType().Name} is not loaded");
        }
    }
}
