using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads draws cards from the deck, tails returns random cards from hand back to the deck.
/// </summary>
public class MysticDraw : ICard<CardUsePayload.MysticDraw> {
    public MysticDraw(ICardConfigs configs, IGameRandom gameRandom, IMoveSnapshotAccessor snapshotAccessor) {
        _configs = configs;
        _gameRandom = gameRandom;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.MysticDraw payload) {
        var config = _configs.Value.MysticDraw_Normal;
        var isHeads = _gameRandom.FlipCoin(invoker);

        _snapshotAccessor.Snapshot.RecordCardUse(invoker.User.Id, _snapshotAccessor.CardId, new CardActionSnapshot.MysticDraw() {
            TargetPlayer = invoker.User.Id,
            IsHeads = isHeads
        });

        if (isHeads) {
            for (var i = 0; i < config.WinDraw; i++) {
                if (invoker.Deck.Count == 0)
                    break;

                var card = invoker.Deck.DrawCard();
                var activeCard = invoker.Hand.Add(card);
                _snapshotAccessor.Snapshot.RecordCardAdd(invoker.User.Id, activeCard.Id, activeCard.Type);
            }
        } else {
            var toReturn = Math.Min(config.LoseReturn, invoker.Hand.Entries.Count);
            for (var i = 0; i < toReturn; i++) {
                var index = _gameRandom.Index(invoker, invoker.Hand.Entries.Count);
                var entry = invoker.Hand.Entries[index];
                invoker.Hand.Remove(entry.Id);
                invoker.Deck.AddCard(entry.Type);
                _snapshotAccessor.Snapshot.RecordCardRemove(invoker.User.Id, entry.Id);
            }
        }

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = null
        };
    }
}
