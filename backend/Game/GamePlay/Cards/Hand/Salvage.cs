using Shared;

namespace Game.GamePlay;

/// <summary>
/// Peeks the top cards from the deck; the player keeps one chosen card and the rest return to the deck.
/// </summary>
public class Salvage : ICard
{
    public Salvage(
        IPlayer owner,
        MoveSnapshot snapshot,
        CardConfigOptions.Salvage config,
        CardUsePayload.Salvage payload)
    {
        _owner = owner;
        _snapshot = snapshot;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly MoveSnapshot _snapshot;
    private readonly CardConfigOptions.Salvage _config;
    private readonly CardUsePayload.Salvage _payload;

    public CardUseResult Use()
    {
        if (_owner.Deck.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Deck is empty"),
                ActionData = null
            };
        }

        var peekCount = Math.Min(_config.PeekCount, _owner.Deck.Count);
        var peeked = new List<CardType>(peekCount);

        for (var i = 0; i < peekCount; i++)
        {
            peeked.Add(_owner.Deck.DrawCard());
        }

        var chosenIndex = Math.Clamp(_payload.ChosenIndex, 0, peeked.Count - 1);

        var chosenCard = peeked[chosenIndex];
        var activeCard = _owner.Hand.Add(chosenCard);
        _snapshot.RecordCardAdd(_owner.User.Id, activeCard.Id, activeCard.Type);

        for (var i = 0; i < peeked.Count; i++)
        {
            if (i == chosenIndex)
                continue;

            _owner.Deck.AddCard(peeked[i]);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Salvage()
            {
                TargetPlayer = _owner.User.Id,
                PeekedCards = peeked,
                ChosenIndex = chosenIndex
            }
        };
    }
}
