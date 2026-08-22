using Internal;

namespace GamePlay.Cards
{
    public interface ICardStateLifetime
    {
        ILifetime OccupyLifetime();
    }

    public class CardStateLifetime : ICardStateLifetime
    {
        public CardStateLifetime(IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;
        }

        private readonly IReadOnlyLifetime _lifetime;

        private ILifetime _current;

        public ILifetime OccupyLifetime()
        {
            _current?.Terminate();
            _current = _lifetime.Child();
            return _current;
        }
    }
}