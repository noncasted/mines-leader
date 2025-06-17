using System;
using Assets.Meta;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Backend;
using Global.Cameras;
using Global.GameLoops;
using Global.GameServices;
using Internal;
using Menu.Loop;
using Menu.Setup;

namespace Loop
{
    public class GameLoop : IGamePlayLoader
    {
        public GameLoop(
            IReadOnlyLifetime lifetime,
            IServiceScopeLoader scopeLoaderFactory,
            IGlobalCamera globalCamera,
            ICurrentCameraProvider currentCameraProvider,
            IBackendProjectionHub backendProjectionHub,
            IUserContext userContext,
            IBackendUser backendUser)
        {
            _lifetime = lifetime;
            _scopeLoaderFactory = scopeLoaderFactory;
            _globalCamera = globalCamera;
            _currentCameraProvider = currentCameraProvider;
            _backendProjectionHub = backendProjectionHub;
            _userContext = userContext;
            _backendUser = backendUser;
        }

        private readonly IServiceScopeLoader _scopeLoaderFactory;
        private readonly IGlobalCamera _globalCamera;
        private readonly ICurrentCameraProvider _currentCameraProvider;
        private readonly IBackendProjectionHub _backendProjectionHub;
        private readonly IUserContext _userContext;
        private readonly IBackendUser _backendUser;
        private readonly IReadOnlyLifetime _lifetime;

        private ILoadedScope _currentScope;
        private ILoadedScope _parent;

        public async UniTask Initialize(ILoadedScope parent)
        {
            _parent = parent;
            
            await _userContext.Init(_lifetime);
            await _backendProjectionHub.Start(_lifetime, _userContext.Id);
            
            await UniTask.WaitUntil(() => _backendUser.Id != Guid.Empty);
            
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
            _currentCameraProvider.SetCamera(_globalCamera.Camera);

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
            _currentCameraProvider.SetCamera(_globalCamera.Camera);

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