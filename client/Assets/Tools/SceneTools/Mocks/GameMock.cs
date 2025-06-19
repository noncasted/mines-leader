using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using UnityEngine;

namespace Tools
{
    public class GameMock : MockBase
    {
        [SerializeField] private GameMode _mode;

        public override async UniTaskVoid Process()
        {
            var scope = await Bootstrap();

            var scopeLoaderFactory = scope.Get<IServiceScopeLoader>();
            var matchmaking = scope.Get<Matchmaking>();

            var sessionData = _mode switch
            {
                GameMode.Single => await matchmaking.CreateGame(scope.Lifetime),
                GameMode.PvP => await matchmaking.SearchGame(scope.Lifetime),
                _ => throw new ArgumentOutOfRangeException()
            };

            switch (_mode)
            {
                case GameMode.Single:
                    await scopeLoaderFactory.ProcessSingleMock(scope, sessionData);
                    break;
                case GameMode.PvP:
                    await scopeLoaderFactory.ProcessPvPMock(scope, sessionData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}