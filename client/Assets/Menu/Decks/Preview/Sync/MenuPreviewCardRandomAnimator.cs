using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using Internal;

namespace Menu.Decks
{
    /// <summary>
    /// No-op <see cref="ICardRandomAnimator"/> for the menu card preview pipeline.
    /// Coin-toss / dice-roll animations have no visual meaning on a preview board,
    /// so we silently complete the tasks and let the preview loop continue.
    /// </summary>
    public sealed class MenuPreviewCardRandomAnimator : ICardRandomAnimator
    {
        public UniTask PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads)
        {
            return UniTask.CompletedTask;
        }

        public UniTask PlayDiceRoll(IReadOnlyLifetime lifetime, int result)
        {
            return UniTask.CompletedTask;
        }
    }
}
