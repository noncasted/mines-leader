using Internal;

namespace GamePlay.Cards
{
    public interface IHand
    {
        IHandPositions Positions { get; }
        IViewableList<ICard> Entries { get; }
        
        void Add(ICard card);
    }
}