using GamePlay.Boards;
using GamePlay.UI;
using Global.Cameras;
using Global.UI;
using Internal;

namespace GamePlay.Services
{
    public class GameServicesInitializer
    {
        public GameServicesInitializer(
            IGlobalCamera globalCamera,
            ILoadingScreen loadingScreen,
            IGameCamera gameCamera,
            IGameOverlay overlay,
            ICellsSelection cellsSelection,
            ICellFlagAction cellFlagAction,
            ICellOpenAction cellOpenAction,
            ICellMultipleOpenAction cellMultipleOpenAction)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _gameCamera = gameCamera;
            _overlay = overlay;
            _cellsSelection = cellsSelection;
            _cellFlagAction = cellFlagAction;
            _cellOpenAction = cellOpenAction;
            _cellMultipleOpenAction = cellMultipleOpenAction;
        }

        private readonly ICellsSelection _cellsSelection;
        private readonly ICellFlagAction _cellFlagAction;
        private readonly ICellOpenAction _cellOpenAction;
        private readonly ICellMultipleOpenAction _cellMultipleOpenAction;

        private readonly IGameOverlay _overlay;

        private readonly IGameCamera _gameCamera;
        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;

        public void Init(IReadOnlyLifetime lifetime)
        {
            _cellsSelection.Start(lifetime);
            _cellFlagAction.Start(lifetime);
            _cellOpenAction.Start(lifetime);
            _cellMultipleOpenAction.Start(lifetime);

            _overlay.Show();

            _gameCamera.Enable();
            _globalCamera.Disable();
            _loadingScreen.Hide();
        }
    }
}