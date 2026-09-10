using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Services
{
    public class LastManStandingRoundSnapshotHandler : ISnapshotHandler<LastManStandingRoundRecord>
    {
        public LastManStandingRoundSnapshotHandler(IGameRound round)
        {
            _round = round;
        }

        private readonly IGameRound _round;

        public UniTask Handle(LastManStandingRoundRecord record)
        {
            ((ILastManStandingRound)_round).Apply(record.CurrentPlayer, record.CurrentRound, record.SecondsLeft);
            return UniTask.CompletedTask;
        }
    }
}
