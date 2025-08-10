using System;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Players;
using GamePlay.Services;
using Global.Cameras;
using Global.UI;
using Internal;
using Meta;

namespace GamePlay.Loop
{
    public interface ISingleGameLoop
    {
        UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData);
    }

    public class SingleGameLoop : ISingleGameLoop
    {
        public SingleGameLoop(
            IUser user,
            INetworkSession session,
            IGameCamera gameCamera,
            ICurrentCamera camera,
            IGameContext gameContext,
            ICellsSelection cellsSelection,
            ICellFlagAction cellFlagAction,
            ICellOpenAction cellOpenAction,
            IBoardMines boardMines,
            IGameFlow gameFlow, IGlobalCamera globalCamera, ILoadingScreen loadingScreen)
        {
            _user = user;
            _session = session;
            _gameCamera = gameCamera;
            _camera = camera;
            _gameContext = gameContext;
            _cellsSelection = cellsSelection;
            _cellFlagAction = cellFlagAction;
            _cellOpenAction = cellOpenAction;
            _boardMines = boardMines;
            _gameFlow = gameFlow;
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
        }

        private readonly IUser _user;
        private readonly INetworkSession _session;
        private readonly IGameCamera _gameCamera;
        private readonly ICurrentCamera _camera;
        private readonly IGameContext _gameContext;
        private readonly ICellsSelection _cellsSelection;
        private readonly ICellFlagAction _cellFlagAction;
        private readonly ICellOpenAction _cellOpenAction;
        private readonly IBoardMines _boardMines;
        private readonly IGameFlow _gameFlow;

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;

        public async UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData)
        {
            _camera.SetCamera(_gameCamera.Camera);

            await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId, _user.Id);

            _cellsSelection.Start(lifetime);
            _cellFlagAction.Start(lifetime);
            _cellOpenAction.Start(lifetime);
            _boardMines.Start(lifetime);

            _loadingScreen.Hide();
            _globalCamera.Disable();

            await _gameFlow.Execute(lifetime);

            await UniTask.Delay(TimeSpan.FromDays(12), cancellationToken: lifetime.Token);
        }
    }
}