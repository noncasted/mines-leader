using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Global.Systems;
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
            IGamePlayer player,
            CardLocalStashOptions options)
        {
            _updater = updater;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _targets = targets;
            _player = player;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardTargets _targets;
        private readonly IGamePlayer _player;
        private readonly CardLocalStashOptions _options;

        public async UniTask Enter(IReadOnlyLifetime lifetime)
        {
            var stateLifetime = _stateLifetime.OccupyLifetime();
            await _player.Turns.IsTurn.WaitFalse(lifetime);

            if (stateLifetime.IsTerminated == true)
                return;

            await CardStashMotion.Play(
                _updater,
                stateLifetime,
                _transform,
                _targets.LocalStash,
                _options);
        }
    }
}
