using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Cameras;
using Global.UI;
using Internal;
using Menu.Common;
using Shared;

namespace Flow.Loop
{
    public class GameLoop : IScopeLoaded
    {
        public GameLoop(
            IMenuLoader menuLoader,
            IGamePlayLoader gamePlayLoader,
            ILoadingScreen loadingScreen,
            IGlobalCamera globalCamera)
        {
            _menuLoader = menuLoader;
            _gamePlayLoader = gamePlayLoader;
            _loadingScreen = loadingScreen;
            _globalCamera = globalCamera;
        }

        private readonly IMenuLoader _menuLoader;
        private readonly IGamePlayLoader _gamePlayLoader;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IGlobalCamera _globalCamera;

        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            Loop(lifetime).Forget();
        }

        private UniTask Loop(IReadOnlyLifetime lifetime)
        {
            Menu().Forget();

            return UniTask.CompletedTask;

            async UniTask Menu()
            {
                var menuResult = await _menuLoader.Load();

                Game(menuResult).Forget();
            }

            async UniTask Game(GameLoadData loadData)
            {
                var transition = await _gamePlayLoader.Load(loadData);

                switch (transition)
                {
                    case GameEndTransition.Exit exit:
                    {
                        _globalCamera.Enable();
                        await _loadingScreen.Show();
                        Menu().Forget();
                        break;
                    }
                    case GameEndTransition.Rematch rematch:
                    {
                        Game(new GameLoadData()
                                    {
                                        Result = new SharedMatchmaking.MatchResult()
                                        {
                                            ServerUrl = rematch.NewSession.ServerUrl,
                                            SessionId = rematch.NewSession.SessionId,
                                            Type = loadData.Result.Type
                                        }
                                    }
                                )
                            .Forget();

                        break;
                    }
                }
            }
        }
    }
}