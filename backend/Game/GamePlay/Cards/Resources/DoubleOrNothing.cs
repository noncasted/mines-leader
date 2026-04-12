using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads doubles current mana, tails sets mana to zero.
/// </summary>
public class DoubleOrNothing : ICard<CardUsePayload.DoubleOrNothing> {
    public DoubleOrNothing(IGameRandom gameRandom, IRoundActionService roundActionService, IMoveSnapshotAccessor snapshotAccessor) {
        _gameRandom = gameRandom;
        _roundActionService = roundActionService;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly IGameRandom _gameRandom;
    private readonly IRoundActionService _roundActionService;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.DoubleOrNothing payload) {
        var isHeads = _gameRandom.FlipCoin(invoker);
        int resultMana;

        if (isHeads) {
            resultMana = invoker.Mana.Current * 2;
        } else {
            resultMana = 0;
        }

        _snapshotAccessor.Snapshot.RecordCardUse(invoker.User.Id, _snapshotAccessor.CardId, new CardActionSnapshot.DoubleOrNothing() {
            TargetPlayer = invoker.User.Id,
            IsHeads = isHeads,
            ResultMana = resultMana
        });

        if (isHeads) {
            var bonus = invoker.Mana.Current;
            invoker.Modifiers.Inc(PlayerModifier.AdditionalMana, bonus);
            invoker.Mana.SetCurrent(resultMana);

            _roundActionService.Schedule(
                new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, bonus), 1);
        } else {
            invoker.Mana.SetCurrent(0);
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = null
        };
    }
}
