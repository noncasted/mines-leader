using Context;
using Shared;

namespace Game.GamePlay;

public interface ICardFactory
{
    IBoardCard CreateBoard(IPlayer owner, CardType type);
}

public class CardFactory : ICardFactory
{
    public CardFactory(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;

    public IBoardCard CreateBoard(IPlayer owner, CardType type)
    {
        var target = SelectBoard(owner, type);
        
        return type switch
        {
            CardType.Trebuchet => new Trebuchet(owner, target, type),
            CardType.Trebuchet_Max => new Trebuchet(owner, target, type),
            // CardType.Bloodhound => new Bloodhound(owner, target),
            // CardType.Bloodhound_Max => new Bloodhound(owner, target),
            // CardType.ErosionDozer => new ErosionDozer(owner, target),
            // CardType.ErosionDozer_Max => new ErosionDozer(owner, target),
            // CardType.ZipZap => new ZipZap(owner, target),
            // CardType.ZipZap_Max => new ZipZap(owner, target),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    IBoard SelectBoard(IPlayer owner, CardType type)
    {
        return type switch
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

        IBoard Opponent()
        {
            return _gameContext.GetOpponent(owner).Board;
        }
    }
}