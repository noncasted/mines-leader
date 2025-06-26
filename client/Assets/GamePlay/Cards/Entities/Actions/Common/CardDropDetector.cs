using Cysharp.Threading.Tasks;
using GamePlay.Services;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardDropDetector
    {
        UniTask<bool> Wait(IReadOnlyLifetime lifetime);
    }

    public class CardDropDetector : ICardDropDetector
    {
        private readonly IGameInput _input;
        private readonly ICardPointerHandler _pointerHandler;

        public CardDropDetector(IGameInput input, ICardPointerHandler pointerHandler)
        {
            _input = input;
            _pointerHandler = pointerHandler;
        }

        public async UniTask<bool> Wait(IReadOnlyLifetime lifetime)
        {
            await _pointerHandler.IsPressed.WaitFalse(lifetime);

            if (_input.World.y < -3)
                return false;
            
            return true;
        }
    }
}