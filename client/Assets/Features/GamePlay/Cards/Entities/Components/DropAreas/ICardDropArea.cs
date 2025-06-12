using Cysharp.Threading.Tasks;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardDropArea
    {
        UniTask<CardDropData> Show(
            IReadOnlyLifetime stateLifetime,
            IReadOnlyLifetime selectionLifetime,
            ICardDropPattern pattern);
    }
}