using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using Internal;
using UnityEngine;

namespace GamePlay.Cards.Drop
{
    public interface ICardRemoteDrop
    {
        UniTask Enter(IReadOnlyLifetime lifetime, Vector2? dropPosition);
    }

    public class CardRemoteDrop : ICardRemoteDrop
    {
        public CardRemoteDrop(
            IUpdater updater,
            ICardTransform transform,
            ICardRenderer renderer,
            ICardStateLifetime stateLifetime,
            ICardDropTarget target)
        {
            _updater = updater;
            _transform = transform;
            _renderer = renderer;
            _stateLifetime = stateLifetime;
            _target = target;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly ICardRenderer _renderer;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardDropTarget _target;

        public UniTask Enter(IReadOnlyLifetime lifetime, Vector2? dropPosition)
        {
            var options = GamePlayAssets.CardRemoteDropOptions;

            _stateLifetime.OccupyLifetime();

            var stackIndex = _target.DroppedCount;
            var endPosition = _target.ReservePosition();
            var twistAngle = Random.Range(options.TwistAngleRange.x, options.TwistAngleRange.y);

            return CardDropMotion.Play(
                _updater,
                lifetime,
                _transform,
                _renderer,
                endPosition,
                twistAngle,
                stackIndex,
                options,
                dropPosition);
        }
    }
}
