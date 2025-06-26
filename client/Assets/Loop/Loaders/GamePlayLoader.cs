using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Cameras;
using Menu.Common;
using VContainer;

namespace Loop
{
    public interface IGamePlayLoader
    {
        UniTask Load(MenuResult menuResult);
    }
    
    public class GamePlayLoader :IGamePlayLoader
    {
        public GamePlayLoader(IGameLoopScopeLoader scopeLoader, IGlobalCamera globalCamera)
        {
            _scopeLoader = scopeLoader;
            _globalCamera = globalCamera;
        }
        
        private readonly IGameLoopScopeLoader _scopeLoader;
        private readonly IGlobalCamera _globalCamera;
        
        public async UniTask Load(MenuResult menuResult)
        {
            _globalCamera.Enable();
            var scope = await _scopeLoader.Load(PvPScopeExtensions.LoadPvp);
            var loop = scope.Container.Container.Resolve<IPvPGameLoop>();
            await loop.Process(scope.Lifetime, menuResult.SessionData);
            _globalCamera.Disable();
        }
    }
}