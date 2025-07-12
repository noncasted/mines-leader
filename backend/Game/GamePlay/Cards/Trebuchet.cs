using Shared;

namespace Game.GamePlay;

public class Trebuchet : ICard
{
    public Trebuchet(
        IPlayer owner,
        IBoard target,
        CardUsePayload.Trebuchet payload)
    {
        _owner = owner;
        _target = target;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly CardUsePayload.Trebuchet _payload;

    public EmptyResponse Use()
    {
        var size = _payload.Type.GetSize() + (int)_owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0)
            return EmptyResponse.Fail("No free cells in the pattern");

        var minesTargets = new List<ICell>();
        var cellsByY = selected.GroupBy(cell => cell.Position.y)
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

        return EmptyResponse.Ok;
    }
}