using Cysharp.Threading.Tasks;
using GamePlay.Loop;
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
        private readonly IGameContext _gameContext;

        public CardDropDetector(
            IGameInput input,
            ICardPointerHandler pointerHandler,
            IGameContext gameContext)
        {
            _input = input;
            _pointerHandler = pointerHandler;
            _gameContext = gameContext;
        }

        public async UniTask<bool> Wait(IReadOnlyLifetime lifetime)
        {
            await _pointerHandler.IsPressed.WaitFalse(lifetime);

            if (_gameContext.IsPaused == true)
                return false;

            if (_input.World.y < -3)
                return false;

            return true;
        }
    }
}