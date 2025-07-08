using System.Collections.Generic;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using GamePlay.Services;
using Global.Backend;
using Global.Cameras;
using Global.UI;
using Internal;
using Meta;

namespace GamePlay.Loop
{
    public interface IPvPGameLoop
    {
        UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData);
    }
    
    public class PvPGameLoop : IPvPGameLoop
    {
        public PvPGameLoop(
            IGlobalCamera globalCamera,
            ILoadingScreen loadingScreen,
            IUser user,
            INetworkSession session,
            IGameCamera gameCamera,
            ICurrentCamera camera,
            IGamePlayerFactory playerFactory,
            IGameContext gameContext,
            ICellsSelection cellsSelection,
            ICellFlagAction cellFlagAction,
            ICellOpenAction cellOpenAction,
            IBoardMines boardMines,
            IGameFlow gameFlow,
            INetworkSocket socket)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _user = user;
            _session = session;
            _gameCamera = gameCamera;
            _camera = camera;
            _playerFactory = playerFactory;
            _gameContext = gameContext;
            _cellsSelection = cellsSelection;
            _cellFlagAction = cellFlagAction;
            _cellOpenAction = cellOpenAction;
            _boardMines = boardMines;
            _gameFlow = gameFlow;
            _socket = socket;
        }

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IUser _user;
        private readonly INetworkSession _session;
        private readonly IGameCamera _gameCamera;
        private readonly ICurrentCamera _camera;
        private readonly IGamePlayerFactory _playerFactory;
        private readonly IGameContext _gameContext;
        private readonly ICellsSelection _cellsSelection;
        private readonly ICellFlagAction _cellFlagAction;
        private readonly ICellOpenAction _cellOpenAction;
        private readonly IBoardMines _boardMines;
        private readonly IGameFlow _gameFlow;
        private readonly INetworkSocket _socket;

        public async UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData)
        {
            _camera.SetCamera(_gameCamera.Camera);
            await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId, _user.Id);

            var localPlayerTask = _playerFactory.CreateLocal(lifetime);
            var remotePlayerTask = _playerFactory.WaitRemote(lifetime, 1);

            var (localPlayer, remotePlayers) = await UniTask.WhenAll(localPlayerTask, remotePlayerTask);

            var players = new List<IGamePlayer>() { localPlayer };
            players.AddRange(remotePlayers);
            
            _gameContext.CompleteSetup(players);

            _cellsSelection.Start(lifetime);
            _cellFlagAction.Start(lifetime);
            _cellOpenAction.Start(lifetime);
            _boardMines.Start(lifetime);
            
            _loadingScreen.Hide();
            _globalCamera.Disable();

            await _gameFlow.Execute(lifetime);
            await _socket.ForceSendAll();
        }
    }
}