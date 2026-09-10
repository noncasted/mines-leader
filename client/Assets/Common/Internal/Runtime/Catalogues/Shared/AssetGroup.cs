using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // Общая база каталожных групп: ретейн-счётчик и защита от обращения к незагруженной группе.
    public abstract class AssetGroup
    {
        private int _retainCount;

        public bool IsLoaded { get; private set; }

        // Вложенные лоадеры статических групп зовутся Loader, поэтому имя для логов и профайлера
        // берём отсюда, а не из GetType().
        public virtual string Name => GetType().Name;

        public async UniTask Retain()
        {
            _retainCount++;

            if (IsLoaded)
                return;

            await LoadGroup();
            IsLoaded = true;
        }

        public void Release()
        {
            if (_retainCount == 0)
            {
                Debug.LogError($"[{Name}] Release called with retain count 0");
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

        public void EnsureLoaded()
        {
            if (IsLoaded == false)
                throw new InvalidOperationException($"{Name} is not loaded");
        }
    }
}