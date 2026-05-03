using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Global.Systems;
using Internal;
using Shared;
using UnityEngine;
using VContainer;

namespace Menu.Screens.Cards.Preview
{
    [DisallowMultipleComponent]
    public class MenuBoard : MonoBehaviour, IMenuBoard, ISceneService, IScopeSetup
    {
        [SerializeField] private Board _board;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private RenderTexture _previewTexture;

        private IUpdater _updater;
        private IBoardActions _actions;

        public IBoard Board => _board;
        public Camera PreviewCamera => _previewCamera;
        public RenderTexture PreviewTexture => _previewTexture;

        [Inject]
        private void Construct(IUpdater updater)
        {
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

        public UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, ICardActionData data, Position? fallback = null)
        {
            IReadOnlyList<Position> positions = data?.TargetCells;

            if ((positions == null || positions.Count == 0) && fallback.HasValue)
                positions = new[] { fallback.Value };

            return PlayCellsAnimation(lifetime, positions, (visuals, lt) => visuals.PlayCellTarget(lt));
        }

        public UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, ICardActionData data)
        {
            // Only play the "cell action" overlay on cells that were actually opened — skipping
            // this for pure-effect/mine-planter cards keeps the overlay from masking the Smoke /
            // Frost / Blackout / Fog visuals that the Sync step is about to apply.
            var positions = data?.OpenedCells?.Select(o => o.Position).ToList();
            return PlayCellsAnimation(lifetime, positions, (visuals, lt) => visuals.PlayCellAction(lt));
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

            var animators = _board.GetComponentsInChildren<CellAnimator>(includeInactive: true);

            foreach (var cellAnimator in animators)
                cellAnimator.SetSprite(null);

            var visuals = _board.GetComponentsInChildren<CellVisuals>(includeInactive: true);

            foreach (var cellVisuals in visuals)
                cellVisuals.SetSprite(null);
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

        public void ApplyUpdatedCells(ICardActionData data)
        {
            if (_board == null || data == null)
                return;

            var updates = data.UpdatedFreeCells ?? data.OpenedCells;

            if (updates == null)
                return;

            foreach (var opened in updates)
            {
                var key = opened.Position.ToVector();

                if (_board.Cells.TryGetValue(key, out var cell) == false)
                    continue;

                var free = cell.EnsureFree();
                free.OnMinesUpdated(opened.MinesAround);
            }
        }

        private async UniTask PlayCellsAnimation(
            IReadOnlyLifetime lifetime,
            IReadOnlyList<Position> cells,
            Func<CellVisuals, IReadOnlyLifetime, UniTask> play)
        {
            if (cells == null || cells.Count == 0 || _board == null)
                return;

            var tasks = new List<UniTask>();

            foreach (var position in cells)
            {
                var key = position.ToVector();

                if (_board.Cells.TryGetValue(key, out var cell) == false)
                    continue;

                if (cell is CellView cellView)
                    tasks.Add(play(cellView.Visuals, lifetime));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
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
        UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, ICardActionData data, Position? fallback = null);
        UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, ICardActionData data);
        void ApplyUpdatedCells(ICardActionData data);
    }
}