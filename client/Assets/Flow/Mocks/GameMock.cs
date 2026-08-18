using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace Flow
{
    public class GameMock : MockBase
    {
        [SerializeField] private GameMatchType _mode;

        public override async UniTaskVoid Process()
        {
            var scope = await Bootstrap();

            var scopeLoaderFactory = scope.Resolve<IServiceScopeLoader>();
            var matchmaking = scope.Resolve<IMatchmaking>();

            var sessionData = await matchmaking.CreateGameWithBot(scope.Lifetime, _mode);
            var gameScope = await scopeLoaderFactory.LoadPvPMock(scope, sessionData);

            var loop = gameScope.Resolve<IPvPGameLoop>();
            await loop.Process(gameScope.Lifetime, sessionData);
        }
    }
}