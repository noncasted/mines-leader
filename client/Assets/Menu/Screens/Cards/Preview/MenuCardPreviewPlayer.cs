using System;
using Cysharp.Threading.Tasks;
using Internal;
using Menu.Screens.Cards.Preview.Sync;
using Shared;
using UnityEngine;
// ReSharper disable once RedundantUsingDirective
using GamePlay.Cards;

namespace Menu.Screens.Cards.Preview
{
    public sealed class MenuCardPreviewPlayer : IMenuCardPreviewPlayer, IScopeSetup
    {
        private const int LoopDelayMs = 350;
        private const int InterActionDelayMs = 200;

        public MenuCardPreviewPlayer(
            IMenuBoard menuBoard,
            IMenuCardPreviewCache cache,
            MenuCardActionSyncRegistry syncRegistry,
            MenuPreviewVfxFactory vfxFactory)
        {
            _menuBoard = menuBoard;
            _cache = cache;
            _syncRegistry = syncRegistry;
            _vfxFactory = vfxFactory;
        }

        private readonly IMenuBoard _menuBoard;
        private readonly IMenuCardPreviewCache _cache;
        private readonly MenuCardActionSyncRegistry _syncRegistry;
        private readonly MenuPreviewVfxFactory _vfxFactory;

        private IReadOnlyLifetime _scopeLifetime;
        private Lifetime _activeLifetime;
        private UniTask _activeTask;
        private bool _hasActiveTask;

        public RenderTexture PreviewTexture => _menuBoard?.PreviewTexture;

        public bool HasPreview(CardType cardType)
        {
            return _cache != null && _cache.TryGet(cardType, out _);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _scopeLifetime = lifetime;
        }

        public void Play(CardType cardType)
        {
            if (_scopeLifetime == null)
            {
                return;
            }

            // Step 1: terminate the previous preview's lifetime + capture its task so the
            // new run can await unwind. Step 2: full board cleanup (effects, reticles, VFX).
            // Done UNCONDITIONALLY — even if the new card has no bundle, we still stop the
            // old one cleanly instead of leaving it running.
            var previousLifetime = _activeLifetime;
            var previousTask = _hasActiveTask ? _activeTask : default;
            var hadPrevious = _hasActiveTask;

            previousLifetime?.Terminate();
            _activeLifetime = null;

            _menuBoard.ResetPreview();
            _vfxFactory?.ClearSpawned();

            if (_cache.TryGet(cardType, out var bundle) == false)
            {
                _hasActiveTask = hadPrevious;
                _activeTask = previousTask;
                return;
            }

            var actions = bundle?.Actions?.Count ?? 0;
            var cells = bundle?.InitialState?.Cells?.Count ?? 0;

            var newLifetime = new Lifetime(_scopeLifetime);
            _activeLifetime = newLifetime;
            _activeTask = RunSerialAsync(newLifetime, bundle, hadPrevious ? previousTask : default, hadPrevious);
            _hasActiveTask = true;
            _activeTask.Forget();
        }

        public void Stop()
        {
            if (_activeLifetime == null && _hasActiveTask == false)
                return;

            _activeLifetime?.Terminate();
            _activeLifetime = null;

            // Full cleanup on hover-leave too: VFX, reticles, effect overlays. Without this,
            // spawned ZipZap lines / frozen target reticles stay on screen after the pointer
            // leaves the card until the user hovers another previewable card.
            _menuBoard.ResetPreview();
            _vfxFactory?.ClearSpawned();
        }

        private async UniTask RunSerialAsync(Lifetime lifetime, CardPreviewBundle bundle, UniTask previous, bool hadPrevious)
        {
            if (hadPrevious)
            {
                try
                {
                    await previous;
                }
                catch
                {
                    // previous run's cancellation / errors are already logged inside RunLoopAsync
                }
            }

            if (lifetime.IsTerminated == true)
                return;

            await RunLoopAsync(lifetime, bundle);
        }

        private async UniTask RunLoopAsync(Lifetime lifetime, CardPreviewBundle bundle)
        {
            try
            {
                while (lifetime.IsTerminated == false)
                {
                    // Reset at the top of EVERY iteration (not just on card change): effects,
                    // reticles and spawned VFX from the previous cycle all die here so the next
                    // playthrough starts from a clean board.
                    _menuBoard.ResetPreview();
                    _vfxFactory?.ClearSpawned();

                    _menuBoard.ApplyInitialState(bundle.InitialState);

                    await UniTask.Delay(LoopDelayMs / 2, cancellationToken: lifetime.Token);

                    if (bundle.Actions != null)
                    {
                        var index = 0;
                        foreach (var action in bundle.Actions)
                        {
                            if (lifetime.IsTerminated == true)
                                return;

                            // Gameplay's CardActionSnapshotHandler plays target + action cell
                            // animations before invoking card.Use(data) — we mirror that here so
                            // every card gets its generic target/opened-cell animation, then the
                            // card-specific Snapshot (ZipZap lightning, Smoke fog, …) runs on top.
                            // Effect cards (Smoke/Blackout/Frost/…) have empty TargetCells in
                            // their ICardActionData — fall back to the bundle's single target.
                            var targetFallback = (action?.TargetCells == null || action.TargetCells.Count == 0)
                                ? bundle.Target
                                : (Position?)null;

                            await _menuBoard.PlayTargetAnimation(lifetime, action, targetFallback);

                            if (lifetime.IsTerminated == true)
                                return;

                            await _menuBoard.PlayActionAnimation(lifetime, action);

                            if (lifetime.IsTerminated == true)
                                return;

                            await _syncRegistry.Dispatch(lifetime, action);

                            if (lifetime.IsTerminated == true)
                                return;

                            await UniTask.Delay(InterActionDelayMs, cancellationToken: lifetime.Token);
                            index++;
                        }
                    }

                    if (bundle.FinalState != null)
                    {
                        _menuBoard.ApplyFinalState(bundle.FinalState);
                    }

                    await UniTask.Delay(LoopDelayMs, cancellationToken: lifetime.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }
    }
}
