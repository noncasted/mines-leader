using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Cards
{
    public interface ICardAction
    {
        UniTask<CardActionResult> TryUse(IReadOnlyLifetime lifetime);
    }

    public class CardActionResult
    {
        public bool IsSuccess { get; set; }
        public ICardUsePayload Payload { get; set; }
    }
}