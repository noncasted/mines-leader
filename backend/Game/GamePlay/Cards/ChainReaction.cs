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
        if (_target.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Target board has no cells"),
                ActionData = null
            };
        }

        var mines = FindMines(_payload.Position);
        var spawnedMines = new List<Position>();
        var spawnPattern = PatternShapes.Rhombus(_config.SpawnSize);

        foreach (var mine in mines)
        {
            var candidates = spawnPattern.SelectTaken(_target, mine.Position);
            foreach (var cell in candidates)
            {
                if (cell.HasMine)
                    continue;

                cell.SetMine();
                spawnedMines.Add(cell.Position);
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
    }

    private IReadOnlyList<ITakenCell> FindMines(Position start)
    {
        var result = new List<ITakenCell>();
        var toProcess = new Queue<Position>();
        var visited = new HashSet<Position>();

        toProcess.Enqueue(start);

        while (toProcess.Count > 0 && result.Count < _config.MaxChain)
        {
            var pos = toProcess.Dequeue();

            if (visited.Contains(pos))
                continue;

            visited.Add(pos);

            if (_target.Cells.TryGetValue(pos, out var cell) == false)
                continue;

            if (cell.IsTaken() == false)
                continue;

            var taken = cell.ToTaken();

            if (taken.HasMine)
                result.Add(taken);

            foreach (var neighbor in GetNeighbors(pos))
                toProcess.Enqueue(neighbor);
        }

        return result;
    }

    private static IReadOnlyList<Position> GetNeighbors(Position position)
    {
        return new[]
        {
            new Position(position.x - 1, position.y),
            new Position(position.x + 1, position.y),
            new Position(position.x, position.y - 1),
            new Position(position.x, position.y + 1),
        };
    }
}
