using Shared;

namespace Game.GamePlay;

public partial class MoveSnapshot
{
    public void RecordCardUse(Guid playerId, Guid cardId, ICardActionData data)
    {
        var effect = CardActionLog.Describe(data);

        if (effect != null)
            SessionLogger?.LogCardEffect(playerId, data.GetType().Name, effect);

        Append(new PlayerSnapshotRecord.CardUse()
        {
            PlayerId = playerId,
            CardId = cardId,
            Data = data,
            HasDropPosition = HasDropPosition,
            DropX = DropX,
            DropY = DropY
        });
    }

    public ICardActionData? TakeCardUseFrom(int startIndex)
    {
        ICardActionData? data = null;

        for (var i = _records.Count - 1; i >= startIndex; i--)
        {
            if (_records[i] is not PlayerSnapshotRecord.CardUse cardUse)
                continue;

            data = cardUse.Data;
            _records.RemoveAt(i);
        }

        return data;
    }

    public void RecordCardAdd(Guid playerId, Guid cardId, CardType type, bool isStash = false)
    {
        Append(new PlayerSnapshotRecord.CardAdd()
        {
            PlayerId = playerId,
            CardId = cardId,
            Type = type,
            IsStash = isStash
        });
    }

    public void RecordCardRemove(Guid playerId, Guid cardId, bool isStash = false)
    {
        Append(new PlayerSnapshotRecord.CardRemove()
        {
            PlayerId = playerId,
            CardId = cardId,
            IsStash = isStash
        });
    }

    public void RecordDeckUpdate(IPlayer player)
    {
        Append(new PlayerSnapshotRecord.DeckUpdate
        {
            PlayerId = player.User.Id,
            Count = player.Deck.Count
        });
    }

    public void RecordCardsStashed(IPlayer player)
    {
        Append(new PlayerSnapshotRecord.CardsStashed
        {
            PlayerId = player.User.Id
        });
    }

    public void RecordStashUpdate(IPlayer player)
    {
        Append(new PlayerSnapshotRecord.StashUpdate
        {
            PlayerId = player.User.Id,
            Count = player.Stash.Count
        });
    }
}
