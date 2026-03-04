using Shared;

namespace Game.GamePlay;

public class ZipZap : ICard
{
    public ZipZap(
        IPlayer owner,
        IBoard target,
        MoveSnapshot snapshot,
        CardConfigOptions.ZipZap config,
        CardUsePayload.ZipZap payload)
    {
        _owner = owner;
        _target = target;
        _snapshot = snapshot;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;
    private readonly MoveSnapshot _snapshot;
    private readonly CardConfigOptions.ZipZap _config;
    private readonly CardUsePayload.ZipZap _payload;

    public CardUseResult Use()
    {
        var size = _config.Size + (int)_owner.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);
        var searchShape = PatternShapes.Rhombus(_config.SearchRadius);

        var selected = pattern.SelectFree(_target, _payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        var targets = new List<ITakenCell>();
        var current = SelectTarget(_payload.Position);

        if (current == null)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No target found in the pattern"),
                ActionData = null
            };
        }

        targets.Add(current);

        for (var i = 1; i < size; i++)
        {
            current = SelectTarget(current.Position);

            if (current == null)
                break;

            targets.Add(current);
        }

        if (targets.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No targets found in the pattern"),
                ActionData = null
            };
        }

        _snapshot.Lock();

        foreach (var target in targets)
            target.ToFree();

        _snapshot.Unlock();

        foreach (var target in targets)
            _target.Revealer.Reveal(target.Position);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ZipZap()
            {
                TargetPlayer = _target.OwnerId,
                Targets = targets.Select(t => t.Position).ToList()
            }
        };
        
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
    }
}