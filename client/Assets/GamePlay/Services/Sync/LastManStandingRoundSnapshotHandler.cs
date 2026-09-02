using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
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
