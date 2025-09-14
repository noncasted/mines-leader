using System;
using Cysharp.Threading.Tasks;
using Internal;
using Menu.Common;
using Meta;

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

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            while (lifetime.IsTerminated == false)
            {
                var menuResult = await _menuLoader.Load();
                
                switch (menuResult.GameMode)
                {
                    case GameMode.Single:
                        break;
                    case GameMode.PvP:
                        var transitionData = await _gamePlayLoader.Load(menuResult);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // async UniTask Menu()
            // {
            //     var menuResult = await _menuLoader.Load();
            // }
            //
            // async UniTask Game(GameLoadData loadData)
            // {
            //     var transitionData = await _gamePlayLoader.Load(loadData);
            //
            //     if (transitionData.ShouldRematch == true)
            //     {
            //         // Game(new GameLoadData()
            //         //     {
            //         //         GameMode = loadData.GameMode,
            //         //         SessionData = new SessionData()
            //         //     }
            //         // );
            //     }
            // }
        }
    }
}