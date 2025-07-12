using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Services;
using Global.Backend;
using Global.Cameras;
using Global.UI;
using Internal;
using Meta;
using Shared;

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
            IGameContext gameContext,
            ICellsSelection cellsSelection,
            ICellFlagAction cellFlagAction,
            ICellOpenAction cellOpenAction,
            IGameFlow gameFlow,
            INetworkConnection connection)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _user = user;
            _session = session;
            _gameCamera = gameCamera;
            _camera = camera;
            _gameContext = gameContext;
            _cellsSelection = cellsSelection;
            _cellFlagAction = cellFlagAction;
            _cellOpenAction = cellOpenAction;
            _gameFlow = gameFlow;
            _connection = connection;
        }

        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IUser _user;
        private readonly INetworkSession _session;
        private readonly IGameCamera _gameCamera;
        private readonly ICurrentCamera _camera;
        private readonly IGameContext _gameContext;
        private readonly ICellsSelection _cellsSelection;
        private readonly ICellFlagAction _cellFlagAction;
        private readonly ICellOpenAction _cellOpenAction;
        private readonly IGameFlow _gameFlow;
        private readonly INetworkConnection _connection;

        public async UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData)
        {
            _camera.SetCamera(_gameCamera.Camera);
            await _session.Start(lifetime, sessionData.ServerUrl, sessionData.SessionId, _user.Id);

            await UniTask.WaitUntil(() => _gameContext.All.Count == 2, cancellationToken: lifetime.Token);

            _cellsSelection.Start(lifetime);
            _cellFlagAction.Start(lifetime);
            _cellOpenAction.Start(lifetime);

            _loadingScreen.Hide();
            _globalCamera.Disable();

            _connection.OneWay(new PlayerReadyContext());

            await _gameFlow.Execute(lifetime);
            await _connection.ForceSendAll();
        }
    }
}