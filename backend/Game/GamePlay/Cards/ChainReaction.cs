using Shared;

namespace Game.GamePlay;

public class ChainReaction : ICard
{
    public ChainReaction(
        IBoard target,
        CardUsePayload.ChainReaction payload,
        CardConfigOptions.ChainReaction config)
    {
        _target = target;
        _payload = payload;
        _config = config;
    }

    private readonly IBoard _target;
    private readonly CardUsePayload.ChainReaction _payload;
    private readonly CardConfigOptions.ChainReaction _config;

    public CardUseResult Use()
    {
        if (_target.Cells.TryGetValue(_payload.Position, out var cell) == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"No cell at position {_payload.Position}"),
                ActionData = null
            };
        }

        if (cell.IsTaken() == false || cell.AsTaken().HasMine == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Target cell has no mine"),
                ActionData = null
            };
        }

        var searchShape = PatternShapes.Rhombus(_config.SearchRadius);
        var spawnShape = PatternShapes.Rhombus(_config.SpawnSize);

        var targets = new List<ITakenCell> { cell.AsTaken() };

        for (var i = 1; i < _config.MaxChain; i++)
        {
            var next = SelectMine(targets[^1].Position);

            if (next == null)
                break;

            targets.Add(next);
        }

        var spawnedMines = new List<Position>();

        foreach (var mine in targets)
        {
            var candidates = spawnShape.SelectAll(_target, mine.Position);

            foreach (var candidate in candidates)
            {
                switch (candidate.Status)
                {
                    case CellStatus.Free:
                        candidate.ToTaken().SetMine();
                        spawnedMines.Add(candidate.Position);
                        break;
                    case CellStatus.Taken:
                        if (candidate.AsTaken().HasMine)
                            continue;

                        candidate.AsTaken().SetMine();
                        spawnedMines.Add(candidate.Position);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        _target.OnUpdated();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ChainReaction
            {
                TargetPlayer = _target.OwnerId,
                SpawnedMines = spawnedMines
            }
        };

        ITakenCell? SelectMine(Position center)
        {
            return searchShape.SelectTaken(_target, center)
                .Where(x => x.HasMine && x.IsFlagged == false && targets.Contains(x) == false)
                .OrderBy(x => x.Position.DistanceTo(center))
                .FirstOrDefault();
        }
    }
}
