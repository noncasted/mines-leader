using Cysharp.Threading.Tasks;
using GamePlay.UI;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.Loop
{
    public interface IGamePlayLoop
    {
        UniTask<IGameEndTransition> Process(IReadOnlyLifetime lifetime, SharedMatchmaking.MatchResult sessionData);
    }

    public class GamePlayLoop : IGamePlayLoop
    {
        public GamePlayLoop(
            IProfile profile,
            INetworkSession session,
            IGameContext gameContext,
            IGameState gameState,
            INetworkConnection connection,
            IGameEnd gameEnd,
            IEventLoop eventLoop,
            GameServicesInitializer servicesInitializer)
        {
            _profile = profile;
            _session = session;
            _gameContext = gameContext;
            _gameState = gameState;
            _connection = connection;
            _gameEnd = gameEnd;
            _eventLoop = eventLoop;
            _servicesInitializer = servicesInitializer;
        }

        private readonly IProfile _profile;
        private readonly INetworkSession _session;
        private readonly INetworkConnection _connection;
        private readonly IGameContext _gameContext;

        private readonly IGameState _gameState;
        private readonly IGameEnd _gameEnd;
        private readonly IEventLoop _eventLoop;
        private readonly GameServicesInitializer _servicesInitializer;

        public async UniTask<IGameEndTransition> Process(
            IReadOnlyLifetime lifetime,
            SharedMatchmaking.MatchResult sessionData)
        {
            _gameState.Set(GameStateType.WaitingFoPlayers);

            await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId, _profile.Id);

            await UniTask.WaitUntil(() => _gameContext.All.Count == 2, cancellationToken: lifetime.Token);
            var localPlayer = _gameContext.Self;
            var remotePlayer = _gameContext.Other;

            _eventLoop.RunCustom<ILocalPlayerCreated>(lifetime, l => l.OnLocalPlayer(lifetime, localPlayer));
            _eventLoop.RunCustom<IRemotePlayerCreated>(lifetime, l => l.OnRemotePlayer(lifetime, remotePlayer));

            _eventLoop.RunCustom<IPlayersCreated>(lifetime,
                l => l.OnPlayersCreated(lifetime, localPlayer, remotePlayer));

            _eventLoop.RunCustom<IGameStarted>(lifetime, l => l.OnGameStarted(lifetime));
            
            _servicesInitializer.Init(lifetime);

            Debug.Log("[Game] All players connected. Starting the match...");
            _connection.OneWay(new MatchActionContexts.PlayerReady());
            _gameState.Set(GameStateType.Active);

            var gameResult = await _gameState.WaitCompletion(lifetime);
            await _connection.ForceSendAll();

            Debug.Log($"[Game] Match completed with result: {gameResult.Type}");

            _gameState.Set(GameStateType.Completed);
            var transition = await _gameEnd.Process(lifetime, gameResult);
            return transition;
        }
    }
}