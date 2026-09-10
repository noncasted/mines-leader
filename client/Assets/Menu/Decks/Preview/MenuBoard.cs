using GamePlay.Boards;
using GamePlay.Cards;
using Internal;
using Shared;
using UnityEngine;

namespace Menu.Decks
{
    [DisallowMultipleComponent]
    public class MenuBoard : MonoBehaviour, IMenuBoard, ISceneService, IScopeSetup
    {
        [SerializeField] private Board _board;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private RenderTexture _previewTexture;

        private IUpdater _updater;
        private IBoardActions _actions;
        private IGameRandom _random;

        public IBoard Board => _board;
        public Camera PreviewCamera => _previewCamera;
        public RenderTexture PreviewTexture => _previewTexture;

        [Inject]
        internal void Construct(IUpdater updater, IGameRandom random)
        {
            _random = random;
            _updater = updater;
            _actions = new PreviewBoardActions();
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuBoard>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            if (_board == null)
                return;

            _board.OnSetup(lifetime);

            foreach (var cell in _board.Cells.Values)
            {
                if (cell is CellView cellView)
                    cellView.Setup(_updater, _actions);
            }

            if (_previewCamera != null && _previewTexture != null)
                _previewCamera.targetTexture = _previewTexture;
        }

        public void ApplyInitialState(BoardLayoutSnapshot state)
        {
            if (_board == null || state == null)
                return;

            // Note: cleanup (effects, CellVisuals overlays, VFX) is done by ResetPreview in
            // MenuCardPreviewPlayer.Play — NOT on every loop iteration. That keeps effects
            // visible across iterations, only wiping when the user hovers a different card.
            var applied = 0;

            foreach (var layoutCell in state.Cells)
            {
                var key = layoutCell.Position.ToVector();

                if (_board.Cells.TryGetValue(key, out var cell) == false)
                    continue;

                switch (layoutCell.Kind)
                {
                    case PreviewCellKind.Taken:
                    case PreviewCellKind.TakenMine:
                        cell.EnsureTaken().OnFlagUpdated(false);
                        applied++;
                        break;
                    case PreviewCellKind.TakenFlag:
                    case PreviewCellKind.TakenFlagMine:
                        cell.EnsureTaken().OnFlagUpdated(true);
                        applied++;
                        break;
                    case PreviewCellKind.Free:
                        cell.EnsureFree().OnMinesUpdated(layoutCell.MinesAround);
                        applied++;
                        break;
                }
            }
        }

        public void ResetPreview()
        {
            // Comprehensive reset between preview cards. Three-prong:
            //  1) Directly disable every CellEffect prefab under the board (Smoke/Frost/Blackout
            //     /Fog/ThermalVision/MineHighlight overlays) — most direct kill.
            //  2) Toggle each CellEffects host off/on and call ForceTerminate on its
            //     GameObjectLifetime so child lifetimes & their Listen callbacks get wiped.
            //  3) Disable every CellVisuals target/action reticle overlay (can freeze on a
            //     mid-animation frame if the previous lifetime was cancelled).
            var lifetimes = _board.GetComponentsInChildren<GameObjectLifetime>(includeInactive: true);

            foreach (var objectLifetime in lifetimes)
            {
                var isActive = objectLifetime.gameObject.activeInHierarchy;

                objectLifetime.gameObject.SetActive(false);
                objectLifetime.ForceTerminate();

                objectLifetime.gameObject.SetActive(isActive);
            }

            _random.ResetViews();

            var cells = _board.GetComponentsInChildren<CellView>(includeInactive: true);

            foreach (var cellView in cells)
                cellView.ResetState();

            var animators = _board.GetComponentsInChildren<CellAnimator>(includeInactive: true);

            foreach (var cellAnimator in animators)
                cellAnimator.SetSprite(null);

            var visuals = _board.GetComponentsInChildren<CellVisuals>(includeInactive: true);

            foreach (var cellVisuals in visuals)
                cellVisuals.SetSprite(null);

            var flags = _board.GetComponentsInChildren<FlagAnimator>(includeInactive: true);

            foreach (var flagAnimator in flags)
                flagAnimator.SetSprite(null);
        }

        public void ApplyFinalState(BoardLayoutSnapshot state)
        {
            // Ships the post-action board layout. Crucially: does NOT clear cell effects like
            // ApplyInitialState does — effects (Smoke/Frost/Blackout/Fog/…) were just applied by
            // the ICardActionSync pipeline and need to remain visible until the loop restarts.
            if (_board == null || state == null)
                return;

            foreach (var layoutCell in state.Cells)
            {
                var key = layoutCell.Position.ToVector();

                if (_board.Cells.TryGetValue(key, out var cell) == false)
                    continue;

                switch (layoutCell.Kind)
                {
                    case PreviewCellKind.Taken:
                    case PreviewCellKind.TakenMine:
                        cell.EnsureTaken().OnFlagUpdated(false);
                        break;
                    case PreviewCellKind.TakenFlag:
                    case PreviewCellKind.TakenFlagMine:
                        cell.EnsureTaken().OnFlagUpdated(true);
                        break;
                    case PreviewCellKind.Free:
                        cell.EnsureFree().OnMinesUpdated(layoutCell.MinesAround);
                        break;
                }
            }
        }
    }

    public interface IMenuBoard
    {
        IBoard Board { get; }
        Camera PreviewCamera { get; }
        RenderTexture PreviewTexture { get; }

        void ApplyInitialState(BoardLayoutSnapshot state);
        void ApplyFinalState(BoardLayoutSnapshot state);
        void ResetPreview();
    }
}