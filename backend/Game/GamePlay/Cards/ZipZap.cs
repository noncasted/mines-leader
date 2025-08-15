using Shared;

namespace Game.GamePlay;

public class ZipZap : ICard
{
    public ZipZap(
        IPlayer owner,
        IBoard target,
        MoveSnapshot snapshot,
        CardUsePayload.ZipZap payload)
    {
        _owner = owner;
        _target = target;
        _snapshot = snapshot;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly MoveSnapshot _snapshot;
    private readonly CardUsePayload.ZipZap _payload;

    public EmptyResponse Use()
    {
        var size = _payload.Type.GetSize() + (int)_owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);
        var searchShape = PatternShapes.Rhombus(CardsConfigs.ZipZap.SearchRadius);

        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0)
            return EmptyResponse.Fail("No taken cells in the pattern");

        var targets = new List<ITakenCell>();
        var current = SelectTarget(_payload.Position);

        if (current == null)
            return EmptyResponse.Fail("No target found in the pattern");

        targets.Add(current);

        for (var i = 1; i < size; i++)
        {
            current = SelectTarget(current.Position);

            if (current == null)
                break;

            targets.Add(current);
        }

        if (targets.Count == 0)
            return EmptyResponse.Fail("No targets found in the pattern");

        _snapshot.Lock();

        foreach (var target in targets)
            target.ToFree();

        _snapshot.Unlock();

        _snapshot.RecordCard(_owner.User.Id, new CardActionSnapshot.ZipZap()
            {
                Targets = targets.Select(t => t.Position).ToList()
            }
        );

        foreach (var target in targets)
            _target.Revealer.Reveal(target.Position);

        ITakenCell? SelectTarget(Position center)
        {
            var searchPositions = searchShape.SelectTaken(_target, center);
            var hasMine = searchPositions.Where(x => x.HasMine == true);
            var hasFlags = hasMine.Where(x => x.IsFlagged == false);
            var unique = hasFlags.Where(x => targets.Contains(x) == false);

            var ordered = unique
                .OrderBy(x => x.Position.DistanceTo(center))
                .ToList();

            if (ordered.Count == 0)
                return null;

            return ordered.First();
        }

        return EmptyResponse.Ok;
    }
}