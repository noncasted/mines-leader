using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Backend;
using Global.Cameras;
using Global.GameLoops;
using Internal;
using Menu.Loop;
using Menu.Setup;
using Meta;

namespace Loop
{
    public class GameLoop : IGamePlayLoader
    {
        public GameLoop(
            IReadOnlyLifetime lifetime,
            IServiceScopeLoader scopeLoaderFactory,
            IGlobalCamera globalCamera,
            ICurrentCamera currentCamera,
            IBackendProjectionHub backendProjectionHub)
        {
            _lifetime = lifetime;
            _scopeLoaderFactory = scopeLoaderFactory;
            _globalCamera = globalCamera;
            _currentCamera = currentCamera;
            _backendProjectionHub = backendProjectionHub;
        }

        private readonly IServiceScopeLoader _scopeLoaderFactory;
        private readonly IGlobalCamera _globalCamera;
        private readonly ICurrentCamera _currentCamera;
        private readonly IBackendProjectionHub _backendProjectionHub;
        private readonly IReadOnlyLifetime _lifetime;

        private ILoadedScope _currentScope;
        private ILoadedScope _parent;

        public async UniTask Initialize(ILoadedScope parent)
        {
            _parent = parent;

            Loop().Forget();
        }

        private async UniTask Loop()
        {
            while (_lifetime.IsTerminated == false)
            {
                var menuResult = await LoadMain();

                switch (menuResult.GameMode)
                {
                    case GameMode.Single:
                        break;
                    case GameMode.PvP:
                        await LoadPvP(_lifetime, menuResult.SessionData);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private async UniTask<MenuResult> LoadMain()
        {
            _globalCamera.Enable();
            _currentCamera.SetCamera(_globalCamera.Camera);

            var unloadTask = UniTask.CompletedTask;

            if (_currentScope != null)
                unloadTask = _currentScope.Dispose();

            var (loadResult, menuResult) = await _scopeLoaderFactory.ProcessMenu(_parent);

            await unloadTask;
            _currentScope = loadResult;
            _globalCamera.Disable();

            return menuResult;
        }

        private async UniTask LoadPvP(IReadOnlyLifetime lifetime, SessionData data)
        {
            _globalCamera.Enable();
            _currentCamera.SetCamera(_globalCamera.Camera);

            var unloadTask = UniTask.CompletedTask;

            if (_currentScope != null)
                unloadTask = _currentScope.Dispose();

            var loadResult = await _scopeLoaderFactory.ProcessPvP(_parent, data);

            await unloadTask;
            _currentScope = loadResult;
            _globalCamera.Disable();
        }
    }
}