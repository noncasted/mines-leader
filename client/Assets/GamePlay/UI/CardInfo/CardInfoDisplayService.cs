using GamePlay.Cards;
using GamePlay.Loop;
using Internal;

namespace GamePlay.UI
{
    public interface ICardInfoDisplayService
    {
        void Init(IReadOnlyLifetime lifetime);
    }

    public class CardInfoDisplayService : ICardInfoDisplayService
    {
        private readonly CardInfoDisplayUI _ui;
        private readonly IGameContext _gameContext;

        public CardInfoDisplayService(CardInfoDisplayUI ui, IGameContext gameContext)
        {
            _ui = ui;
            _gameContext = gameContext;
        }

        public void Init(IReadOnlyLifetime lifetime)
        {
            var hand = _gameContext.Self.Hand;

            // Subscribe to hand entries changes
            hand.Entries.View(lifetime, (_, card) =>
                {
                    SubscribeToCard(lifetime, card);
                });
        }

        private void SubscribeToCard(IReadOnlyLifetime lifetime, ICard card)
        {
            if (card is not ILocalCard localCard)
                return;

            var cardLifetime = lifetime.Child();
            card.Lifetime.Listen(() => cardLifetime.Terminate());

            localCard.PointerHandler.IsHovered.Advise(cardLifetime, isHovered =>
                {
                    if (isHovered && !localCard.IsInSpawnAnimation.Value)
                    {
                        _ui.DisplayCard(
                            card.Definition.Name,
                            card.Definition.Description);
                    }
                    else if (localCard.IsInSpawnAnimation.Value)
                    {
                        _ui.HideImmediately();
                    }
                    else
                    {
                        _ui.Hide();
                    }
                });

            localCard.IsInSpawnAnimation.Advise(cardLifetime, isSpawning =>
                {
                    if (isSpawning)
                    {
                        _ui.HideImmediately();
                    }
                });
        }
    }
}