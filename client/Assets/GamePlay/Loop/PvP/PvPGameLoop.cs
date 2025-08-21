using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.UI;
using Global.Backend;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Loop
{
    public interface IPvPGameLoop
    {
        UniTask<IGameEndTransition> Process(IReadOnlyLifetime lifetime, SessionData sessionData);
    }

    public class PvPGameLoop : IPvPGameLoop
    {
        public PvPGameLoop(
            IUser user,
            INetworkSession session,
            IGameContext gameContext,
            IGameState gameState,
            INetworkConnection connection,
            IGameEnd gameEnd,
            GameServicesInitializer servicesInitializer)
        {
            _user = user;
            _session = session;
            _gameContext = gameContext;
            _gameState = gameState;
            _connection = connection;
            _gameEnd = gameEnd;
            _servicesInitializer = servicesInitializer;
        }

        private readonly IUser _user;
        private readonly INetworkSession _session;
        private readonly INetworkConnection _connection;
        private readonly IGameContext _gameContext;

        private readonly IGameState _gameState;
        private readonly IGameEnd _gameEnd;
        private readonly GameServicesInitializer _servicesInitializer;

        public async UniTask<IGameEndTransition> Process(IReadOnlyLifetime lifetime, SessionData sessionData)
        {
            _gameState.Set(GameStateType.WaitingFoPlayers);

            await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId, _user.Id);

            await UniTask.WaitUntil(() => _gameContext.All.Count == 2, cancellationToken: lifetime.Token);
    
            _servicesInitializer.Init(lifetime);
            
            _connection.OneWay(new MatchActionContexts.PlayerReady());
            _gameState.Set(GameStateType.Active);

            var gameResult = await _gameState.WaitCompletion(lifetime);
            await _connection.ForceSendAll();

            _gameState.Set(GameStateType.Completed);
            var transition = await _gameEnd.Process(lifetime, gameResult);
            return transition;
        }
    }
}