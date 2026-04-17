using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
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
