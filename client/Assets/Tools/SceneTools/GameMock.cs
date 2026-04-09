using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using VContainer;

namespace Tools
{
    public class GameMock : MockBase
    {
        [SerializeField] private GameMatchType _mode;
        [SerializeField] private int _startMaxMana = 10;

        public override async UniTaskVoid Process()
        {
            var scope = await Bootstrap();

            var scopeLoaderFactory = scope.Resolve<IServiceScopeLoader>();
            var matchmaking = scope.Resolve<IMatchmaking>();

            var sessionData = await matchmaking.CreateGameWithBot(scope.Lifetime, _mode);
            var gameScope = await scopeLoaderFactory.LoadPvPMock(scope, sessionData);
            
            var context = gameScope.Resolve<IGameContext>();
            var connection = gameScope.Resolve<INetworkConnection>();
            var loop = gameScope.Resolve<IPvPGameLoop>();

            var lifetime = this.GetObjectLifetime();

            context.Updated.Advise(lifetime, () => {
                if (context.Self == null)
                    return;

                var manaLifetime = lifetime.Child();

                context.Self.Mana.Max.View(manaLifetime, max => {
                    if (max == 0)
                        return;

                    manaLifetime.Terminate();
                    connection.Request(new GameCheatContexts.ChangeMaxMana { Value = _startMaxMana });
                });
            });

            await loop.Process(gameScope.Lifetime, sessionData);
        }
    }
}