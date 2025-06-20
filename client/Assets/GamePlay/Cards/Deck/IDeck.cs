using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface IDeck
    {
        UniTask DrawCard(IReadOnlyLifetime lifetime);
    }
}