namespace GamePlay.Cards
{
    public class HandEntryHandle : IHandEntryHandle
    {
        public HandEntryHandle(IHand hand, ICard card)
        {
            _hand = hand;
            _card = card;
        }
     
        private readonly IHand _hand;
        private readonly ICard _card;
        
        private ICardPositionHandle _positionHandle;
        
        public ICardPositionHandle PositionHandle => _positionHandle;

        public void AddToHand()
        {
            _hand.Add(_card);
            _positionHandle = _hand.Positions.GetPositionHandle(_card);
        }
    }
}