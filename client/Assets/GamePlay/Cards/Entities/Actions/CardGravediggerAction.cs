using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public class CardGravediggerAction : ICardAction
    {
        public CardGravediggerAction(IStash stash, ICardDropDetector dropDetector, ICardUseSync useSync)
        {
            _stash = stash;
            _dropDetector = dropDetector;
            _useSync = useSync;
        }

        private readonly IStash _stash;
        private readonly ICardDropDetector _dropDetector;
        private readonly ICardUseSync _useSync;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            if (isDropped == false || _stash.IsEmpty == true || lifetime.IsTerminated == true)
                return false;

            _stash.DrawCard(lifetime).Forget();
            _useSync.Send(new CardUseEvents.Gravedigger());

            return true;
        }
    }
    
    public class CardGravediggerActionSync : ICardActionSync
    {
        public UniTask ShowOnRemote(IReadOnlyLifetime lifetime, ICardUseEvent payload)
        {
            return UniTask.CompletedTask;
        }
    }
}