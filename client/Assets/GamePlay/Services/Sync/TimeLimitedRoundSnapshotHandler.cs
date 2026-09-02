using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class TimeLimitedRoundSnapshotHandler : ISnapshotHandler<TimeLimitedRoundRecord>
    {
        public TimeLimitedRoundSnapshotHandler(ITimeLimitedGameRound round)
        {
            _round = round;
        }

        private readonly ITimeLimitedGameRound _round;

        public UniTask Handle(TimeLimitedRoundRecord record)
        {
            _round.Apply(record.CurrentPlayer, record.SecondsLeft);
            return UniTask.CompletedTask;
        }
    }
}
