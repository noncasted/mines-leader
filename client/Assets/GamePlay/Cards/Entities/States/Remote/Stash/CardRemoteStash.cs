using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardRemoteStash : ICardStash
    {
    }

    public class CardRemoteStash : ICardRemoteStash
    {
        public CardRemoteStash(
            IUpdater updater,
            ICardTransform transform,
            ICardStateLifetime stateLifetime,
            ICardTargets targets)
        {
            _updater = updater;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _targets = targets;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardTargets _targets;

        public async UniTask Enter(IReadOnlyLifetime lifetime)
        {
            var options = GamePlayAssets.CardRemoteStashOptions;

            if (lifetime.IsTerminated == true)
                return;

            var stateLifetime = _stateLifetime.OccupyLifetime();

            await CardStashMotion.Play(
                _updater,
                stateLifetime,
                _transform,
                _targets.RemoteStash,
                options);
        }
    }
}
