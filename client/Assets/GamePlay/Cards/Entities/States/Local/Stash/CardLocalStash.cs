using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardLocalStash : ICardStash
    {
    }

    public class CardLocalStash : ICardLocalStash
    {
        public CardLocalStash(
            IUpdater updater,
            ICardTransform transform,
            ICardStateLifetime stateLifetime,
            ICardTargets targets,
            IGamePlayer player)
        {
            _updater = updater;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _targets = targets;
            _player = player;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardTargets _targets;
        private readonly IGamePlayer _player;

        public async UniTask Enter(IReadOnlyLifetime lifetime)
        {
            var options = GamePlayAssets.CardLocalStashOptions;

            await _player.Turns.IsTurn.WaitFalse(lifetime);

            if (lifetime.IsTerminated == true)
                return;

            var stateLifetime = _stateLifetime.OccupyLifetime();

            await CardStashMotion.Play(
                _updater,
                stateLifetime,
                _transform,
                _targets.LocalStash,
                options);
        }
    }
}
