using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public class CardGravediggerAction : ICardAction
    {
        public CardGravediggerAction(IStash stash, ICardDropDetector dropDetector)
        {
            _stash = stash;
            _dropDetector = dropDetector;
        }

        private readonly IStash _stash;
        private readonly ICardDropDetector _dropDetector;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            if (isDropped == false || _stash.IsEmpty == true || lifetime.IsTerminated == true)
                return false;

            _stash.DrawCard(lifetime).Forget();
            return true;
        }
    }
}