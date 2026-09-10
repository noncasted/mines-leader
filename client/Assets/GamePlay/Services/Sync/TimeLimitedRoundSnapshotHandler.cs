using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class TimeLimitedRoundSnapshotHandler : ISnapshotHandler<TimeLimitedRoundRecord>
    {
        public TimeLimitedRoundSnapshotHandler(IGameRound round)
        {
            _round = round;
        }

        private readonly IGameRound _round;

        public UniTask Handle(TimeLimitedRoundRecord record)
        {
            ((ITimeLimitedGameRound)_round).Apply(record.CurrentPlayer, record.SecondsLeft);
            return UniTask.CompletedTask;
        }
    }
}
