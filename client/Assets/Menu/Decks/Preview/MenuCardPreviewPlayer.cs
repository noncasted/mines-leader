using Cysharp.Threading.Tasks;
using GamePlay.Cards;
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

    public sealed class MenuCardPreviewPlayer : IMenuCardPreviewPlayer
    {
        public MenuCardPreviewPlayer(
            IMenuBoard menuBoard,
            IMenuCardPreviewCache cache,
            ICardActionSyncDispatcher syncDispatcher,
            MenuPreviewVfxFactory vfxFactory)
        {
            _menuBoard = menuBoard;
            _cache = cache;
            _syncDispatcher = syncDispatcher;
            _vfxFactory = vfxFactory;
        }

        private const int LoopDelayMs = 1500;
        private const int InterActionDelayMs = 200;

        private readonly IMenuBoard _menuBoard;
        private readonly IMenuCardPreviewCache _cache;
        private readonly ICardActionSyncDispatcher _syncDispatcher;
        private readonly MenuPreviewVfxFactory _vfxFactory;

        public RenderTexture PreviewTexture => _menuBoard?.PreviewTexture;

        public bool HasPreview(CardType cardType)
        {
            return _cache.TryGet(cardType, out _);
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

                    await _syncDispatcher.Dispatch(lifetime, action);

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