using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public class SingleGameFlow : IGameFlow
    {
        public SingleGameFlow(IGameContext context, IGameRound gameRound)
        {
            _context = context;
            _gameRound = gameRound;
        }

        private readonly IGameContext _context;
        private readonly IGameRound _gameRound;
        private readonly UniTaskCompletionSource<GameResult> _completion = new();

        private ILifetime _flowLifetime;

        public async UniTask<GameResult> Execute(IReadOnlyLifetime lifetime)
        {
            _flowLifetime = lifetime.Child();

            return await _completion.Task;
        }

        public void OnLose(IGamePlayer player)
        {
            _flowLifetime.Terminate();
        }

        public void OnWin(IGamePlayer player)
        {
            _flowLifetime.Terminate();
        }

        public void OnLeave()
        {
        }
    }
}