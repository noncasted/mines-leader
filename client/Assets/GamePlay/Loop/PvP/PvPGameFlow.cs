using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public class PvPGameFlow : NetworkService, IGameFlow
    {
        public PvPGameFlow(
            IGameContext context,
            IGameRound gameRound,
            INetworkUsersCollection sessionUsers)
        {
            _context = context;
            _gameRound = gameRound;
            _sessionUsers = sessionUsers;
        }

        private readonly IGameContext _context;
        private readonly IGameRound _gameRound;
        private readonly INetworkUsersCollection _sessionUsers;
        private readonly UniTaskCompletionSource<GameResult> _completion = new();

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            Events.GetEvent<GameFlowEvents.Lose>().Advise(lifetime, context =>
                {
                    if (context.PlayerId == _context.Self.Id)
                        return;

                    OnWin(_context.Self);
                }
            );
        }

        public async UniTask<GameResult> Execute(IReadOnlyLifetime lifetime)
        {
            var flowLifetime = lifetime.Child();
            flowLifetime.Terminate();

            await _completion.Task;

            return new GameResult();
        }

        public void OnLose(IGamePlayer player)
        {
            Events.Send(new GameFlowEvents.Lose(player.Id));

            _completion.TrySetResult(new GameResult()
                {
                    Type = GameResultType.Lose
                }
            );
        }

        public void OnWin(IGamePlayer player)
        {
            _completion.TrySetResult(new GameResult()
                {
                    Type = GameResultType.Win
                }
            );
        }

        public void OnLeave()
        {
            Events.Send(new GameFlowEvents.Lose(_context.Self.Id));

            _completion.TrySetResult(new GameResult()
                {
                    Type = GameResultType.Leave
                }
            );
        }
    }
}