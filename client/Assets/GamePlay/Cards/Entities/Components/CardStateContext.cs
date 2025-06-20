using Internal;

namespace GamePlay.Cards
{
    public interface ICardStateContext
    {
        ILifetime OccupyLifetime();
    }
    
    public class CardStateContext : ICardStateContext
    {
        public CardStateContext(ICard card)
        {
            _card = card;
        }

        private readonly ICard _card;

        private ILifetime _current;

        public ILifetime OccupyLifetime()
        {
            _current?.Terminate();
            _current = _card.Lifetime.Child();
            return _current;
        }
    }
}