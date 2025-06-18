using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Loop
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

        public async UniTask<ILoadedScope> Load(Func<IServiceScopeLoader, ILoadedScope, UniTask<ILoadedScope>> action)
        {
            _currentScope?.Dispose().Forget();
            var currentScope = await action(_serviceScopeLoader, _parentScope);
            _currentScope = currentScope;
            return _currentScope;
        }
    }
}