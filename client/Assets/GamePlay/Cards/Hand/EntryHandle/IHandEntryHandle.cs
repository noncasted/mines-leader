namespace GamePlay.Cards
{
    public interface IHandEntryHandle
    {
        ICardPositionHandle PositionHandle { get; }
        
        void AddToHand();
    }
}