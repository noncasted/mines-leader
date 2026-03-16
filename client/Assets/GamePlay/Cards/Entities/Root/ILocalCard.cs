using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ILocalCard : ICard
    {
        IViewableDelegate Used { get; }
        ICardLocalDrop Drop { get; }
        ICardPointerHandler PointerHandler { get; }
        IViewableProperty<bool> IsInSpawnAnimation { get; }
        void SetSpawning(bool isSpawning);
    }
}