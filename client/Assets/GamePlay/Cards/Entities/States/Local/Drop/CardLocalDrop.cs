using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardLocalDrop
    {
        UniTask Enter(IReadOnlyLifetime lifetime, Vector2? dropPosition);
    }

    public class CardLocalDrop : ICardLocalDrop
    {
        public CardLocalDrop(
            IUpdater updater,
            ICardTransform transform,
            ICardRenderer renderer,
            ICardStateLifetime stateLifetime,
            ICardDropTarget target,
            CardLocalDropOptions options)
        {
            _updater = updater;
            _transform = transform;
            _renderer = renderer;
            _stateLifetime = stateLifetime;
            _target = target;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly ICardRenderer _renderer;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardDropTarget _target;
        private readonly CardLocalDropOptions _options;

        public UniTask Enter(IReadOnlyLifetime lifetime, Vector2? dropPosition)
        {
            _stateLifetime.OccupyLifetime();

            var stackIndex = _target.DroppedCount;
            var endPosition = _target.ReservePosition();
            var twistAngle = Random.Range(_options.TwistAngleRange.x, _options.TwistAngleRange.y);

            return CardDropMotion.Play(
                _updater,
                lifetime,
                _transform,
                _renderer,
                endPosition,
                twistAngle,
                stackIndex,
                _options,
                dropPosition);
        }
    }
}
