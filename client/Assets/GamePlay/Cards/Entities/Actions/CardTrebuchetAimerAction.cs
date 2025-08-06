using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public class CardTrebuchetAimerAction : ICardAction
    {
        public CardTrebuchetAimerAction(
            ICardDropDetector dropDetector,
            IPlayerModifiers modifiers,
            ICardUseSync useSync)
        {
            _dropDetector = dropDetector;
            _modifiers = modifiers;
            _useSync = useSync;
        }

        private readonly ICardDropDetector _dropDetector;
        private readonly IPlayerModifiers _modifiers;
        private readonly ICardUseSync _useSync;

        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            if (isDropped == false || lifetime.IsTerminated == true)
                return false;

            _modifiers.Inc(PlayerModifier.TrebuchetBoost);
            _useSync.Send(new CardUseEvents.TrebuchetAimer());

            return true;
        }
    }
    
    public class CardTrebuchetAimerActionSync : ICardActionSync
    {
        public UniTask ShowOnRemote(IReadOnlyLifetime lifetime, ICardUseEvent payload)
        {
            return UniTask.CompletedTask;
        }
    }
}