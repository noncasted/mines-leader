using GamePlay.Cards.Drop;

namespace GamePlay.Cards
{
    public interface IRemoteCard : ICard
    {
        ICardRemoteDrop Drop { get; }
    }
}