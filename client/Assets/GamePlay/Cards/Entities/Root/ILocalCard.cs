using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ILocalCard : ICard
    {
        IViewableDelegate Used { get; }
        
        UniTask Destroy();
    }
}