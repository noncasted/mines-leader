using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using Internal;
using Shared;
using UnityEngine;

// ReSharper disable once RedundantUsingDirective

namespace Menu.Decks
{
    /// <summary>
    /// Contract of the service that plays a card preview on Menu_Board. The implementation is Agent A.
    /// Agent B (UI) consumes only this interface: starts playback on hover,
    /// stops it on leave, reads the board RenderTexture to substitute it into the popup.
    /// </summary>
    public interface IMenuCardPreviewPlayer
    {
        /// <summary>
        /// The Menu_Board scene RenderTexture. The UI binds it to the popup background-image.
        /// </summary>
        RenderTexture PreviewTexture { get; }

        /// <summary>
        /// True if a preview bundle is generated on the backend for this card (field-modifying cards).
        /// Resource/buff/hand cards without visuals return false - the UI hides the popup by this flag.
        /// </summary>
        bool HasPreview(CardType cardType);

        /// <summary>
        /// Starts the preview scenario for the given card (state reset + playing snapshots).
        /// </summary>
        UniTask Play(IReadOnlyLifetime lifetime, CardType cardType);
    }

    public sealed class MenuCardPreviewPlayer : IMenuCardPreviewPlayer
    {
        public MenuCardPreviewPlayer(
            IMenuBoard menuBoard,
            IMenuCardPreviewCache cache,
            ICardActionSyncDispatcher syncDispatcher)
        {
            _menuBoard = menuBoard;
            _cache = cache;
            _syncDispatcher = syncDispatcher;
        }

        private const int LoopDelayMs = 1500;
        private const int InterActionDelayMs = 200;

        private readonly IMenuBoard _menuBoard;
        private readonly IMenuCardPreviewCache _cache;
        private readonly ICardActionSyncDispatcher _syncDispatcher;

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
        }

        private async UniTask Show(IReadOnlyLifetime lifetime, CardPreviewBundle bundle)
        {
            while (lifetime.IsTerminated == false)
            {
                _menuBoard.ResetPreview();

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