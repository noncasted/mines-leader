using System;
using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Loop
{
    public class PvPGameFlow : NetworkService, IGameFlow
    {
        public PvPGameFlow(
            IGameContext context,
            NetworkProperty<GameFlowState> state)
        {
            _context = context;
            _state = state;
        }

        private readonly IGameContext _context;
        private readonly NetworkProperty<GameFlowState> _state;
        private readonly UniTaskCompletionSource<GameResult> _completion = new();

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _state.Advise(lifetime, state =>
            {
                if (state.Winner == Guid.Empty)
                    return;
                
                var player = _context.GetPlayer(state.Winner);

                _completion.TrySetResult(new GameResult()
                    {
                        Type = player.Info.IsLocal == true ? GameResultType.Win : GameResultType.Lose
                    }
                );
            });
        }

        public UniTask<GameResult> Execute(IReadOnlyLifetime lifetime)
        {
            return _completion.Task;
        }

        public void OnLeave()
        {
            _completion.TrySetResult(new GameResult()
            {
                Type = GameResultType.Leave
            });
        }
    }
}