using Shared;

namespace Game.GamePlay;

public interface ICardFactory
{
    ICard Create(IPlayer owner, MoveSnapshot snapshot, ICardUsePayload payload);
}

public class CardFactory : ICardFactory
{
    public CardFactory(IGameContext gameContext, IRoundActionService roundActionService)
    {
        _gameContext = gameContext;
        _roundActionService = roundActionService;
    }

    private readonly IGameContext _gameContext;
    private readonly IRoundActionService _roundActionService;

    public ICard Create(IPlayer owner, MoveSnapshot snapshot, ICardUsePayload payload)
    {
        return payload.Type switch
        {
            CardType.Trebuchet => new Trebuchet(
                owner,
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Trebuchet)payload
            ),
            CardType.Trebuchet_Max => new Trebuchet(
                owner,
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Trebuchet)payload
            ),
            CardType.Bloodhound => new Bloodhound(
                owner,
                GetBoard(owner, payload),
                (CardUsePayload.Bloodhound)payload
            ),
            CardType.Bloodhound_Max => new Bloodhound(
                owner,
                GetBoard(owner, payload),
                (CardUsePayload.Bloodhound)payload
            ),
            CardType.ZipZap => new ZipZap(
                owner,
                GetBoard(owner, payload),
                snapshot,
                (CardUsePayload.ZipZap)payload
            ),
            CardType.ZipZap_Max => new ZipZap(
                owner,
                GetBoard(owner, payload),
                snapshot,
                (CardUsePayload.ZipZap)payload
            ),
            CardType.TrebuchetAimer => new TrebuchetAimer(
                owner,
                (CardUsePayload.TrebuchetAimer)payload
            ),
            CardType.TrebuchetAimer_Max => new TrebuchetAimer(
                owner,
                (CardUsePayload.TrebuchetAimer)payload
            ),
            CardType.ErosionDozer => new ErosionDozer(
                GetBoard(owner, payload),
                (CardUsePayload.ErosionDozer)payload
            ),
            CardType.ErosionDozer_Max => new ErosionDozer(
                GetBoard(owner, payload),
                (CardUsePayload.ErosionDozer)payload
            ),
            CardType.Gravedigger => new GraveDigger(
                owner,
                snapshot,
                (CardUsePayload.Gravedigger)payload
            ),
            CardType.OpponentBomb => new OpponentBomb(
                _gameContext.GetOpponent(owner),
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentBomb)payload
            ),
            CardType.OpponentFlagErase => new OpponentFlagErase(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentFlagErase)payload
            ),
            CardType.OpponentFlagErase_Max => new OpponentFlagErase(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentFlagErase)payload
            ),
            CardType.OpponentFlagReshuffle => new OpponentFlagReshuffle(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentFlagReshuffle)payload
            ),
            CardType.OpponentFlagReshuffle_Max => new OpponentFlagReshuffle(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentFlagReshuffle)payload
            ),
            CardType.Smoke => new Smoke(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Smoke)payload,
                _roundActionService
            ),
            CardType.Smoke_Max => new Smoke(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Smoke)payload,
                _roundActionService
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(payload.Type), payload.Type, null)
        };
    }

    IBoard GetBoard(IPlayer owner, ICardUsePayload payload)
    {
        if (payload is not IBoardCardUsePayload boardPayload)
            throw new ArgumentException("Payload must implement IBoardCardUsePayload", nameof(payload));

        var board = owner.Board;
        board.EnsureGenerated(boardPayload.Position);

        return board;
    }
}