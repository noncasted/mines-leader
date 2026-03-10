using Shared;

namespace Game.GamePlay;

public class Trebuchet : ICard
{
    public Trebuchet(
        IPlayer owner,
        IBoard target,
        CardConfigOptions.Trebuchet config,
        CardUsePayload.Trebuchet payload)
    {
        _owner = owner;
        _target = target;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardConfigOptions.Trebuchet _config;
    private readonly CardUsePayload.Trebuchet _payload;

    public CardUseResult Use()
    {
        if (_target.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }
        
        var size = _config.Size + (int)_owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        var minesTargets = new List<ICell>();
        var cellsByY = Enumerable.GroupBy<ICell, int>(selected, cell => cell.Position.y)
            .OrderByDescending(group => group.Key);

        foreach (var group in cellsByY)
        {
            if (group.Count() == 1)
            {
                minesTargets.Add(group.First());
            }
            else
            {
                minesTargets.Add(group.First());
                minesTargets.Add(group.Last());
            }
        }

        foreach (var cell in selected)
            cell.ToTaken();

        foreach (var cell in minesTargets)
            cell.ToTaken().SetMine();

        _owner.Modifiers.Reset(PlayerModifier.TrebuchetBoost);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Trebuchet()
            {
                TargetPlayer = _target.OwnerId
            }
        };
    }
}