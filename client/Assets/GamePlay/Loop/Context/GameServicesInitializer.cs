using System;
using GamePlay.Boards;
using GamePlay.Services;
using GamePlay.UI;
using Global.Cameras;
using Global.UI;
using Internal;
using UnityEngine;

namespace GamePlay.Loop
{
    public class GameServicesInitializer
    {
        public GameServicesInitializer(
            IGlobalCamera globalCamera,
            ILoadingScreen loadingScreen,
            IGameCamera gameCamera,
            IGameOverlayUI overlayUI,
            ICellsSelection cellsSelection,
            ICellFlagAction cellFlagAction,
            ICellOpenAction cellOpenAction,
            ICellMultipleOpenAction cellMultipleOpenAction,
            ICardInfoDisplayService cardInfoDisplayService)
        {
            _globalCamera = globalCamera;
            _loadingScreen = loadingScreen;
            _gameCamera = gameCamera;
            _overlayUI = overlayUI;
            _cellsSelection = cellsSelection;
            _cellFlagAction = cellFlagAction;
            _cellOpenAction = cellOpenAction;
            _cellMultipleOpenAction = cellMultipleOpenAction;
            _cardInfoDisplayService = cardInfoDisplayService;
        }

        private readonly ICellsSelection _cellsSelection;
        private readonly ICellFlagAction _cellFlagAction;
        private readonly ICellOpenAction _cellOpenAction;
        private readonly ICellMultipleOpenAction _cellMultipleOpenAction;

        private readonly IGameOverlayUI _overlayUI;

        private readonly IGameCamera _gameCamera;
        private readonly IGlobalCamera _globalCamera;
        private readonly ILoadingScreen _loadingScreen;
        private readonly ICardInfoDisplayService _cardInfoDisplayService;

        public void Init(IReadOnlyLifetime lifetime)
        {
            _cellsSelection.Start(lifetime);
            _cellFlagAction.Start(lifetime);
            _cellOpenAction.Start(lifetime);
            _cellMultipleOpenAction.Start(lifetime);

            try
            {
                _cardInfoDisplayService.Init(lifetime);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }

            _overlayUI.Show();

            _gameCamera.Enable();
            _globalCamera.Disable();
            _loadingScreen.Hide();
        }
    }
}