using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace Tools
{
    public class GameMock : MockBase
    {
        [SerializeField] private GameMatchType _mode;

        public override async UniTaskVoid Process()
        {
            var scope = await Bootstrap();

            var scopeLoaderFactory = scope.Get<IServiceScopeLoader>();
            var matchmaking = scope.Get<IMatchmaking>();

            var sessionData = _mode switch
            {
                GameMatchType.Single => await matchmaking.CreateGame(scope.Lifetime),
                GameMatchType.LastManStanding => await matchmaking.SearchGame(scope.Lifetime, GameMatchType.LastManStanding),
                _ => throw new ArgumentOutOfRangeException()
            };

            switch (_mode)
            {
                case GameMatchType.Single:
                    await scopeLoaderFactory.ProcessSingleMock(scope, sessionData);
                    break;
                case GameMatchType.LastManStanding:
                    await scopeLoaderFactory.ProcessPvPMock(scope, sessionData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}