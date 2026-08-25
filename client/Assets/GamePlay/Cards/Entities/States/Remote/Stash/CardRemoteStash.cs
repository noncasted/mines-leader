using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Global.Systems;
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
            ICardTargets targets,
            IGamePlayer player,
            CardRemoteStashOptions options)
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
        private readonly CardRemoteStashOptions _options;

        public async UniTask Enter(IReadOnlyLifetime lifetime)
        {
            await _player.Turns.IsTurn.WaitFalse(lifetime);

            if (lifetime.IsTerminated == true)
                return;

            var stateLifetime = _stateLifetime.OccupyLifetime();

            await CardStashMotion.Play(
                _updater,
                stateLifetime,
                _transform,
                _targets.RemoteStash,
                _options);
        }
    }
}
