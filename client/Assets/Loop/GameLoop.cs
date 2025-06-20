using System;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using UnityEngine;

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
            Debug.Log("On loaded");
            Loop(lifetime).Forget();
        }

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            while (lifetime.IsTerminated == false)
            {
                Debug.Log("Load menu");
                var menuResult = await _menuLoader.Load();

                switch (menuResult.GameMode)
                {
                    case GameMode.Single:
                        break;
                    case GameMode.PvP:
                        await _gamePlayLoader.Load(menuResult);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}