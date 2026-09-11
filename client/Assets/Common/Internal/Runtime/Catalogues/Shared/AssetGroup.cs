using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // Общая база каталожных групп: ретейн-счётчик и защита от обращения к незагруженной группе.
    public abstract class AssetGroup
    {
        private int _retainCount;
        private UniTaskCompletionSource _loading;

        public bool IsLoaded { get; private set; }

        // Вложенные лоадеры статических групп зовутся Loader, поэтому имя для логов и профайлера
        // берём отсюда, а не из GetType().
        public virtual string Name => GetType().Name;

        // Группа не из Addressables лежит в Resources и едет в данных плеера: бандл ей не нужен,
        // первое обращение грузит её синхронно, поэтому Retain необязателен. Выгружать её незачем.
        public virtual bool IsAddressable => true;

        public async UniTask Retain()
        {
            _retainCount++;

            if (IsLoaded)
                return;

            // Группу могут ретейнить параллельно (предзагрузка на старте и скоуп меню): второй
            // вызов ждёт ту же загрузку, а не поднимает второй хендл. Preserve тут не годится —
            // незавершённую задачу он ждать дважды не даёт, а источник завершения даёт.
            if (_loading == null)
            {
                _loading = new UniTaskCompletionSource();
                Load(_loading).Forget();
            }

            await _loading.Task;
        }

        public void Release()
        {
            if (_retainCount == 0)
            {
                Debug.LogError($"[{Name}] Release called with retain count 0");
                return;
            }

            _retainCount--;

            if (_retainCount > 0 || IsAddressable == false)
                return;

            UnloadGroup();
            IsLoaded = false;
            _loading = null;
        }

        public void EnsureLoaded()
        {
            if (IsLoaded)
                return;

            if (IsAddressable)
                throw new InvalidOperationException($"{Name} is not loaded");

            LoadImmediately();
            IsLoaded = true;
        }

        protected abstract UniTask LoadGroup();

        protected abstract void UnloadGroup();

        // Синхронная загрузка есть только у групп из Resources.
        protected virtual void LoadImmediately()
        {
            throw new NotSupportedException($"{Name} is addressable and loads only through Retain");
        }

        private async UniTask Load(UniTaskCompletionSource loading)
        {
            try
            {
                await LoadGroup();
                IsLoaded = true;
                loading.TrySetResult();
            }
            catch (Exception exception)
            {
                // Упавшую загрузку следующий Retain должен повторить, а не получить ту же ошибку.
                if (_loading == loading)
                    _loading = null;

                loading.TrySetException(exception);
            }
        }
    }
}
