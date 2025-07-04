using Internal;

namespace GamePlay.Cards
{
    public interface ICardStateLifetime
    {
        ILifetime OccupyLifetime();
    }
    
    public class CardStateLifetime : ICardStateLifetime
    {
        public CardStateLifetime(ICard card)
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