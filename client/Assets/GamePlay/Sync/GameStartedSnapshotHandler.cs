using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Network;
using Shared;

namespace GamePlay
{
    public class GameStartedSnapshotHandler : ISnapshotHandler<GameStartedRecord>
    {
        public GameStartedSnapshotHandler(
            IGameContext gameContext,
            INetworkConnection connection)
        {
            _gameContext = gameContext;
            _connection = connection;
        }

        private readonly IGameContext _gameContext;
        private readonly INetworkConnection _connection;

        public UniTask Handle(GameStartedRecord record)
        {
            _gameContext.SetGameStarted(record.CardMovesCost);
            _connection.OneWay(new MatchActionContexts.PlayerLoaded());
            return UniTask.CompletedTask;
        }
    }
}
