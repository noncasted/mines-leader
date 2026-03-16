using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public interface ICardFactory
{
    ICard Create(IPlayer owner, MoveSnapshot snapshot, ICardUsePayload payload);
}

public class CardFactory : ICardFactory
{
    public CardFactory(
        IGameContext gameContext,
        IRoundActionService roundActionService,
        ICardConfigs configs)
    {
        _gameContext = gameContext;
        _roundActionService = roundActionService;
        _configs = configs;
    }

    private readonly IGameContext _gameContext;
    private readonly IRoundActionService _roundActionService;
    private readonly ICardConfigs _configs;

    public ICard Create(IPlayer owner, MoveSnapshot snapshot, ICardUsePayload payload)
    {
        return payload.Type switch
        {
            CardType.Trebuchet => new Trebuchet(
                owner,
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.Trebuchet_Normal,
                (CardUsePayload.Trebuchet)payload
            ),
            CardType.Trebuchet_Max => new Trebuchet(
                owner,
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.Trebuchet_Normal,
                (CardUsePayload.Trebuchet)payload
            ),
            CardType.Bloodhound => new Bloodhound(
                GetBoard(owner, payload),
                _configs.Value.BloodHound_Normal,
                (CardUsePayload.Bloodhound)payload
            ),
            CardType.Bloodhound_Max => new Bloodhound(
                GetBoard(owner, payload),
                _configs.Value.BloodHound_Max,
                (CardUsePayload.Bloodhound)payload
            ),
            CardType.ZipZap => new ZipZap(
                owner,
                GetBoard(owner, payload),
                snapshot,
                _configs.Value.ZipZap_Normal,
                (CardUsePayload.ZipZap)payload
            ),
            CardType.ZipZap_Max => new ZipZap(
                owner,
                GetBoard(owner, payload),
                snapshot,
                _configs.Value.ZipZap_Max,
                (CardUsePayload.ZipZap)payload
            ),
            CardType.TrebuchetAimer => new TrebuchetAimer(
                owner,
                _configs.Value.TrebuchetAimer_Normal
            ),
            CardType.TrebuchetAimer_Max => new TrebuchetAimer(
                owner,
                _configs.Value.TrebuchetAimer_Max
            ),
            CardType.ErosionDozer => new ErosionDozer(
                GetBoard(owner, payload),
                _configs.Value.ErosionDozer_Normal,
                (CardUsePayload.ErosionDozer)payload
            ),
            CardType.ErosionDozer_Max => new ErosionDozer(
                GetBoard(owner, payload),
                _configs.Value.ErosionDozer_Max,
                (CardUsePayload.ErosionDozer)payload
            ),
            CardType.Gravedigger => new GraveDigger(owner, snapshot),
            CardType.OpponentBomb => new OpponentBomb(
                _gameContext.GetOpponent(owner),
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentBomb)payload
            ),
            CardType.OpponentFlagErase => new OpponentFlagErase(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagErase_Normal,
                (CardUsePayload.OpponentFlagErase)payload
            ),
            CardType.OpponentFlagErase_Max => new OpponentFlagErase(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagErase_Max,
                (CardUsePayload.OpponentFlagErase)payload
            ),
            CardType.OpponentFlagReshuffle => new OpponentFlagReshuffle(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagReshuffle_Normal,
                (CardUsePayload.OpponentFlagReshuffle)payload
            ),
            CardType.OpponentFlagReshuffle_Max => new OpponentFlagReshuffle(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagReshuffle_Max,
                (CardUsePayload.OpponentFlagReshuffle)payload
            ),
            CardType.Smoke => new Smoke(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Smoke)payload,
                _configs.Value.Smoke_Normal,
                _roundActionService
            ),
            CardType.Smoke_Max => new Smoke(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Smoke)payload,
                _configs.Value.Smoke_Max,
                _roundActionService
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(payload.Type), payload.Type, null)
        };
    }

    IBoard GetBoard(IPlayer owner, ICardUsePayload payload)
    {
        if (payload is not IBoardCardUsePayload boardPayload)
            throw new ArgumentException("Value must implement IBoardCardUsePayload", nameof(payload));

        var board = owner.Board;
        board.EnsureGenerated(boardPayload.Position);

        return board;
    }
}