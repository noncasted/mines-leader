using Cysharp.Threading.Tasks;
using GamePlay.UI;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardRemoteDrop
    {
        UniTask Enter(IReadOnlyLifetime lifetime, Vector2? dropPosition, ICardActionData data);
    }

    public class CardRemoteDrop : ICardRemoteDrop
    {
        public CardRemoteDrop(
            IUpdater updater,
            ICardTransform transform,
            ICardRenderer renderer,
            ICardStateLifetime stateLifetime,
            ICardDropTarget target,
            ICardActionSyncDispatcher actionSync)
        {
            _updater = updater;
            _transform = transform;
            _renderer = renderer;
            _stateLifetime = stateLifetime;
            _target = target;
            _actionSync = actionSync;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly ICardRenderer _renderer;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardDropTarget _target;
        private readonly ICardActionSyncDispatcher _actionSync;

        public async UniTask Enter(IReadOnlyLifetime lifetime, Vector2? dropPosition, ICardActionData data)
        {
            var options = GamePlayAssets.CardRemoteDropOptions;

            var stateLifetime = _stateLifetime.OccupyLifetime();

            var slot = _target.Reserve();
            var twistAngle = Random.Range(options.TwistAngleRange.x, options.TwistAngleRange.y);

            await CardDropMotion.Play(
                _updater,
                stateLifetime,
                _transform,
                _renderer,
                slot,
                twistAngle,
                options,
                dropPosition);

            await _actionSync.Dispatch(lifetime, data);
        }
    }
}
