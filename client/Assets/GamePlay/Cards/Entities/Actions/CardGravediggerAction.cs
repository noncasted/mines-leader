using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public class CardGravediggerAction : ICardAction
    {
        public CardGravediggerAction(ICardDropDetector dropDetector)
        {
            _dropDetector = dropDetector;
        }

        private readonly ICardDropDetector _dropDetector;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            // if (isDropped == false || _stash.IsEmpty == true || lifetime.IsTerminated == true)
            //     return false;
            //
            // _stash.DrawCard(lifetime).Forget();
            // _useSync.Send(new CardUseEvents.Gravedigger());

            return true;
        }
    }
}