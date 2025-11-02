using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Menu.Common;
using Meta;
using Shared;

namespace Loop
{
    public class GameLoop : IScopeLoaded
    {
        public GameLoop(IMenuLoader menuLoader, IGamePlayLoader gamePlayLoader)
        {
            _menuLoader = menuLoader;
            _gamePlayLoader = gamePlayLoader;
        }

        private readonly IMenuLoader _menuLoader;
        private readonly IGamePlayLoader _gamePlayLoader;

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
                        ).Forget();

                        break;
                    }
                }
            }
        }
    }
}