using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

// ReSharper disable once RedundantUsingDirective

namespace Menu.Decks
{
    /// <summary>
    /// Контракт сервиса проигрывания превью карты на Menu_Board. Реализация — Agent A.
    /// Agent B (UI) потребляет только этот интерфейс: стартует воспроизведение на hover,
    /// останавливает на leave, читает RenderTexture доски для подстановки в popup.
    /// </summary>
    public interface IMenuCardPreviewPlayer
    {
        /// <summary>
        /// RenderTexture сцены Menu_Board. UI привязывает её к background-image popup.
        /// </summary>
        RenderTexture PreviewTexture { get; }

        /// <summary>
        /// True, если для этой карты на беке сгенерирован превью-бандл (поле-модифицирующие карты).
        /// Resource/buff/hand-карты без визуала возвращают false — UI по этому флагу скрывает popup.
        /// </summary>
        bool HasPreview(CardType cardType);

        /// <summary>
        /// Запускает сценарий превью для данной карты (сброс стейта + проигрывание снапшотов).
        /// </summary>
        UniTask Play(IReadOnlyLifetime lifetime, CardType cardType);
    }

    public sealed class MenuCardPreviewPlayer : IMenuCardPreviewPlayer, IScopeSetup
    {
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

        private const int LoopDelayMs = 350;
        private const int InterActionDelayMs = 200;

        private readonly IMenuBoard _menuBoard;
        private readonly IMenuCardPreviewCache _cache;
        private readonly MenuCardActionSyncRegistry _syncRegistry;
        private readonly MenuPreviewVfxFactory _vfxFactory;

        private IReadOnlyLifetime _scopeLifetime;

        public RenderTexture PreviewTexture => _menuBoard?.PreviewTexture;

        public bool HasPreview(CardType cardType)
        {
            return _cache.TryGet(cardType, out _);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _scopeLifetime = lifetime;
        }

        public async UniTask Play(IReadOnlyLifetime lifetime, CardType cardType)
        {
            if (_cache.TryGet(cardType, out var bundle) == false)
                return;

            await Show(lifetime, bundle);

            _menuBoard.ResetPreview();
            _vfxFactory.ClearSpawned();
        }

        private async UniTask Show(IReadOnlyLifetime lifetime, CardPreviewBundle bundle)
        {
            while (lifetime.IsTerminated == false)
            {
                _menuBoard.ResetPreview();
                _vfxFactory.ClearSpawned();

                _menuBoard.ApplyInitialState(bundle.InitialState);

                await UniTask.Delay(LoopDelayMs / 2, cancellationToken: lifetime.Token);

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
                }

                _menuBoard.ApplyFinalState(bundle.FinalState);

                await UniTask.Delay(LoopDelayMs, cancellationToken: lifetime.Token);
            }

        }
    }
}