using Internal;

namespace GamePlay.Cards
{
    public interface IHandPositions
    {
        void AddCard(IReadOnlyLifetime lifetime, ICard card);
        ICardPositionHandle GetPositionHandle(ICard card);
    }
}