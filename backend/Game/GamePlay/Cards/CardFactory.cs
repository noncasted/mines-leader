using Shared;

namespace Game.GamePlay;

public interface ICardFactory
{
    ICard Create(IPlayer owner, ICardUsePayload payload);
}

public class CardFactory : ICardFactory
{
    public CardFactory(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public ICard Create(IPlayer owner, ICardUsePayload payload)
    {
        return payload.Type switch
        {
            CardType.Trebuchet => new Trebuchet(
                owner,
                GetPreparedTarget(owner, payload),
                (CardUsePayload.Trebuchet)payload
            ),
            CardType.Trebuchet_Max => new Trebuchet(
                owner,
                GetPreparedTarget(owner, payload),
                (CardUsePayload.Trebuchet)payload
            ),
            // CardType.Bloodhound => new Bloodhound(owner, target),
            // CardType.Bloodhound_Max => new Bloodhound(owner, target),
            // CardType.ErosionDozer => new ErosionDozer(owner, target),
            // CardType.ErosionDozer_Max => new ErosionDozer(owner, target),
            // CardType.ZipZap => new ZipZap(owner, target),
            // CardType.ZipZap_Max => new ZipZap(owner, target),
            _ => throw new ArgumentOutOfRangeException(nameof(payload.Type), payload.Type, null)
        };
    }

    IBoard GetPreparedTarget(IPlayer owner, ICardUsePayload payload)
    {
        if (payload is not IBoardCardUsePayload boardPayload)
            throw new ArgumentException("Payload must implement IBoardCardUsePayload", nameof(payload));

        var board = payload.Type switch
        {
            CardType.Trebuchet => Opponent(),
            CardType.Trebuchet_Max => Opponent(),
            CardType.Bloodhound => owner.Board,
            CardType.Bloodhound_Max => owner.Board,
            CardType.ErosionDozer => owner.Board,
            CardType.ErosionDozer_Max => owner.Board,
            CardType.ZipZap => owner.Board,
            CardType.ZipZap_Max => owner.Board,
            _ => throw new ArgumentOutOfRangeException()
        };

        board.EnsureGenerated(boardPayload.Position);
        return board;

        IBoard Opponent()
        {
            return _gameContext.GetOpponent(owner).Board;
        }
    }
}