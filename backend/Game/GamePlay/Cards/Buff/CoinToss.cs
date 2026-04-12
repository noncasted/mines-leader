using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads grants extra moves, tails costs moves.
/// </summary>
public class CoinToss : ICard<CardUsePayload.CoinToss> {
    public CoinToss(ICardConfigs configs, IGameRandom gameRandom, IRoundActionService roundActionService, IMoveSnapshotAccessor snapshotAccessor) {
        _configs = configs;
        _gameRandom = gameRandom;
        _roundActionService = roundActionService;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;
    private readonly IRoundActionService _roundActionService;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.CoinToss payload) {
        var config = _configs.Value.CoinToss_Normal;
        var isHeads = _gameRandom.FlipCoin(invoker);

        _snapshotAccessor.Snapshot.RecordCardUse(invoker.User.Id, _snapshotAccessor.CardId, new CardActionSnapshot.CoinToss() {
            TargetPlayer = invoker.User.Id,
            IsHeads = isHeads
        });

        if (isHeads) {
            invoker.Modifiers.Inc(PlayerModifier.AdditionalMoves, config.WinMoves);

            _roundActionService.Schedule(
                new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMoves, config.WinMoves), 1);
        } else {
            var newMoves = invoker.Moves.Left - config.LoseMoves;
            if (newMoves < 0) newMoves = 0;
            invoker.Moves.SetCurrent(newMoves);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = null
        };
    }
}
