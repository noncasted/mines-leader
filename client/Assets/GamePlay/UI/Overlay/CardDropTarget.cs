using GamePlay.Cards;
using GamePlay.Loop;
using Internal;
using UnityEngine;

namespace GamePlay.UI
{
    public interface ICardDropTarget
    {
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

    public class CardDropTarget : ICardDropTarget, IScopeSetup
    {
        private readonly RectTransform _transform;
        private readonly IGameRound _round;

        public CardDropTarget(
            IGameRound round,
            RoundOverlayUIBindings bindings)
        {
            _round = round;
            _transform = bindings.Card.RectTransform;
        }

        private int _count;

        // Never reset: cards of the previous turn may still be flying into the stash
        // while the next ones are already landing, and those have to lie on top.
        private int _sortingOrder;

        private Vector2 Position
        {
            get
            {
                var point = _transform.TransformPoint(_transform.rect.center);
                return point;
            }
        }
 
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _round.Player.Advise(lifetime, ResetCount);
        }

        public CardDropSlot Reserve()
        {
            var position = Position + Vector2.up * 0.1f * _count;
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