using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardAction
    {
        UniTask<bool> Execute(IReadOnlyLifetime lifetime);
    }
    
    public interface ICardActionSync
    {
        UniTask ShowOnRemote(IReadOnlyLifetime lifetime, ICardUseEvent payload);
    }
}