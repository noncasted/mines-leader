using System;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Backend;
using Global.GameServices;
using Internal;
using UnityEngine;

namespace Tools
{
    public class GameMock : MockBase
    {
        [SerializeField] private GameMode _mode;

        public override async UniTaskVoid Process()
        {
            var globalScope = await BootstrapGlobal();

            var scopeLoaderFactory = globalScope.Get<IServiceScopeLoader>();
            var matchmaking = globalScope.Get<IBackendMatchmaking>();
            var userContext = globalScope.Get<IUserContext>();
            var backendHub = globalScope.Get<IBackendProjectionHub>();
            var backendUser = globalScope.Get<IBackendUser>();
            
            await userContext.Init(globalScope.Lifetime);
            await backendHub.Start(globalScope.Lifetime, userContext.Id);

            await UniTask.WaitUntil(() => backendUser.Id != Guid.Empty);
            
            var sessionData = _mode switch
            {
                GameMode.Single => await matchmaking.CreateGame(globalScope.Lifetime),
                GameMode.PvP => await matchmaking.SearchGame(globalScope.Lifetime),
                _ => throw new ArgumentOutOfRangeException()
            };

            var localUserList = globalScope.Get<ILocalUserList>();

            await UniTask.WaitUntil(() => localUserList.Count != 0);

            switch (_mode)
            {
                case GameMode.Single:
                    await scopeLoaderFactory.ProcessSingleMock(globalScope, sessionData);
                    break;
                case GameMode.PvP:
                    await scopeLoaderFactory.ProcessPvPMock(globalScope, sessionData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}