using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Cards
{
    public class CardTrebuchetAimerAction : ICardAction
    {
        public CardTrebuchetAimerAction(
            ICardDropDetector dropDetector,
            IPlayerModifiers modifiers)
        {
            _dropDetector = dropDetector;
            _modifiers = modifiers;
        }

        private readonly ICardDropDetector _dropDetector;
        private readonly IPlayerModifiers _modifiers;

        public UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime)
        {
            return UniTask.FromResult(new CardActionResult());
        }
        
        public async UniTask<bool> Execute(IReadOnlyLifetime lifetime)
        {
            var isDropped = await _dropDetector.Wait(lifetime);

            if (isDropped == false || lifetime.IsTerminated == true)
                return false;

            // _modifiers.Inc(PlayerModifier.TrebuchetBoost);
            // _useSync.Send(new CardUseEvents.TrebuchetAimer());

            return true;
        }
    }
}