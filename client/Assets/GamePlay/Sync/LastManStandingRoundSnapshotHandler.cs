using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class LastManStandingRoundSnapshotHandler : ISnapshotHandler<LastManStandingRoundRecord>
    {
        public LastManStandingRoundSnapshotHandler(ILastManStandingRound round)
        {
            _round = round;
        }

        private readonly ILastManStandingRound _round;

        public UniTask Handle(LastManStandingRoundRecord record)
        {
            _round.Apply(record.CurrentPlayer, record.CurrentRound, record.SecondsLeft);
            return UniTask.CompletedTask;
        }
    }
}
