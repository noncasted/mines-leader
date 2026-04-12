using Shared;

namespace Game.GamePlay;

/// <summary>
/// Steals a random card from the opponent's hand and adds it to the owner's hand.
/// </summary>
public class CardThief : ICard<CardUsePayload.CardThief> {
    public CardThief(IGameContext gameContext, IGameRandom gameRandom, IMoveSnapshotAccessor snapshotAccessor) {
        _gameContext = gameContext;
        _gameRandom = gameRandom;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly IGameContext _gameContext;
    private readonly IGameRandom _gameRandom;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.CardThief payload) {
        var opponent = _gameContext.GetOpponent(invoker);
        var handEntries = opponent.Hand.Entries.ToList();

        if (handEntries.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("Opponent has no cards in hand"),
                ActionData = null
            };
        }

        var index = _gameRandom.Index(invoker, handEntries.Count);
        var entry = handEntries[index];

        _snapshotAccessor.Snapshot.RecordCardUse(invoker.User.Id, _snapshotAccessor.CardId, new CardActionSnapshot.CardThief() {
            TargetPlayer = opponent.User.Id,
            StolenCard = entry.Type
        });

        opponent.Hand.Remove(entry.Id);
        _snapshotAccessor.Snapshot.RecordCardRemove(opponent.User.Id, entry.Id);

        var activeCard = invoker.Hand.Add(entry.Type);
        _snapshotAccessor.Snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = null
        };
    }
}
