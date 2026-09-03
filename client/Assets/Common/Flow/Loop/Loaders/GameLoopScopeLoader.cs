using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Flow.Loop
{
    public interface IGameLoopScopeLoader
    {
        UniTask<ILoadedScope> Load(Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action);
    }

    public class GameLoopScopeLoader : IGameLoopScopeLoader
    {
        public GameLoopScopeLoader(
            IServiceScopeLoader serviceScopeLoader,
            ILoadedScope parentScope)
        {
            _serviceScopeLoader = serviceScopeLoader;
            _parentScope = parentScope;
        }

        private readonly IServiceScopeLoader _serviceScopeLoader;
        private readonly ILoadedScope _parentScope;

        private ILoadedScope _currentScope;
        private UniTask _unloading;

        public async UniTask<ILoadedScope> Load(Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action)
        {
            // Скоупы делят имена сервисных сцен, поэтому предыдущий выгружается полностью
            // до начала загрузки нового: иначе SceneManager.CreateScene падает на дубликате
            // имени, а сцены старого скоупа остаются висеть поверх нового.
            await Unload();

            var currentScope = await action(_serviceScopeLoader, _parentScope);
            _currentScope = currentScope;
            return currentScope;
        }

        private UniTask Unload()
        {
            if (_currentScope == null)
                return _unloading;

            var scope = _currentScope;
            _currentScope = null;
            _unloading = scope.Dispose();

            return _unloading;
        }
    }
}
