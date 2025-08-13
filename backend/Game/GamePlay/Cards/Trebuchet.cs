using Common;
using Shared;

namespace Game.GamePlay;

public class Trebuchet : ICard
{
    public Trebuchet(
        IPlayer owner,
        IBoard target,
        CardType cardType
    )
    {
        _owner = owner;
        _target = target;
        _cardType = cardType;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardType _cardType;
    
    private int _minesAmount => _cardType switch
    {
        CardType.Trebuchet => CardsConfigs.Trebuchet.NormalMines,
        CardType.Trebuchet_Max => CardsConfigs.Trebuchet.MaxMines,
        _ => throw new ArgumentOutOfRangeException()
    };
    
    public EmptyResponse Use(ICardUsePayload payload)
    {
        if (payload is not CardUsePayload.Trebuchet trebuchetPayload)
            return EmptyResponse.Fail("Invalid payload type for Trebuchet card");
        
        var size = _cardType.GetSize() + (int)_owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectFree(_target, trebuchetPayload.Position);
        
        if (selected.Count == 0)
            return EmptyResponse.Fail("No free cells in the pattern");
        
        var shuffled = new List<ICell>(selected);
        shuffled.Shuffle();

        for (var index = 0; index < shuffled.Count; index++)
        {
            var cell = shuffled[index];
            var taken = cell.ToTaken();

            if (index < _minesAmount)
                taken.SetMine();
        }

        _owner.Modifiers.Reset(PlayerModifier.TrebuchetBoost);

        return EmptyResponse.Ok;
    }
}