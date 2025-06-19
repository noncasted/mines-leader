using System;
using System.Collections.Generic;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using GamePlay.Services;
using Global.Cameras;
using Internal;
using Meta;

namespace GamePlay.Loop
{
    public class PvPGameLoop : IPvPGameLoop
    {
        public PvPGameLoop(
            INetworkSession session,
            IGameCamera gameCamera,
            ICurrentCameraProvider cameraProvider,
            IGamePlayerFactory playerFactory,
            IGameContext gameContext,
            ICellsSelection cellsSelection,
            ICellFlagAction cellFlagAction,
            ICellOpenAction cellOpenAction,
            IBoardMines boardMines,
            IGameFlow gameFlow)
        {
            _session = session;
            _gameCamera = gameCamera;
            _cameraProvider = cameraProvider;
            _playerFactory = playerFactory;
            _gameContext = gameContext;
            _cellsSelection = cellsSelection;
            _cellFlagAction = cellFlagAction;
            _cellOpenAction = cellOpenAction;
            _boardMines = boardMines;
            _gameFlow = gameFlow;
        }

        private readonly INetworkSession _session;
        private readonly IGameCamera _gameCamera;
        private readonly ICurrentCameraProvider _cameraProvider;
        private readonly IGamePlayerFactory _playerFactory;
        private readonly IGameContext _gameContext;
        private readonly ICellsSelection _cellsSelection;
        private readonly ICellFlagAction _cellFlagAction;
        private readonly ICellOpenAction _cellOpenAction;
        private readonly IBoardMines _boardMines;
        private readonly IGameFlow _gameFlow;

        public async UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData)
        {
            _cameraProvider.SetCamera(_gameCamera.Camera);
            await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId);

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

            await _gameFlow.Execute(lifetime);
            
            await UniTask.Delay(TimeSpan.FromDays(12), cancellationToken: lifetime.Token);
        }
    }
}