using GamePlay.Loop;
using Global.Constants;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    public interface ICardDropTarget
    {
        Vector2 Position { get; }

        CardDropSlot Reserve();
    }

    /// <summary>
    /// A place in the dropped pile: where the card lands and how it is layered
    /// over the cards already lying there.
    /// </summary>
    public readonly struct CardDropSlot
    {
        public CardDropSlot(Vector2 position, int sortingOrder)
        {
            Position = position;
            SortingOrder = sortingOrder;
        }

        public Vector2 Position { get; }
        public int SortingOrder { get; }
    }

    [DisallowMultipleComponent]
    public class CardDropTarget : MonoBehaviour, ICardDropTarget, ISceneService, IScopeSetup
    {
        [SerializeField] private float _cardHeight = GameConstants.PixelSize;

        private IGameRound _round;
        private int _count;

        // Never reset: cards of the previous turn may still be flying into the stash
        // while the next ones are already landing, and those have to lie on top.
        private int _sortingOrder;

        public Vector2 Position
        {
            get
            {
                if (transform is RectTransform rectTransform)
                {
                    var point = rectTransform.TransformPoint(rectTransform.rect.center);

                    // TEMP DEBUG: chasing the editor/build mismatch of the drop landing point.
                    var canvas = GetComponentInParent<Canvas>();
                    Debug.Log($"[DropTarget] point={point} lossyScale={rectTransform.lossyScale} " +
                              $"rect={rectTransform.rect} screen={Screen.width}x{Screen.height} " +
                              $"canvas={(canvas == null ? "null" : canvas.name)} " +
                              $"mode={(canvas == null ? "-" : canvas.renderMode.ToString())} " +
                              $"worldCamera={(canvas == null || canvas.worldCamera == null ? "null" : canvas.worldCamera.name)}");

                    return point;
                }

                return transform.position;
            }
        }

        [Inject]
        internal void Construct(IGameRound round)
        {
            _round = round;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardDropTarget>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _round.Player.Advise(lifetime, ResetCount);
        }

        public CardDropSlot Reserve()
        {
            var position = Position + Vector2.up * _cardHeight * _count;
            _count++;
            _sortingOrder++;

            return new CardDropSlot(position, CardSorting.DroppedOrder + _sortingOrder);
        }

        private void ResetCount()
        {
            _count = 0;
        }
    }
}
