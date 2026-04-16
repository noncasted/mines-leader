using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class ChainReaction : ICard<CardUsePayload.ChainReaction>
{
    public ChainReaction(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ChainReaction payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.TryGetValue(payload.Position, out var cell) == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"No cell at position {payload.Position}"),
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

        var config = _configs.Value.ChainReaction_Normal;
        var searchShape = PatternShapes.Rhombus(config.SearchRadius);
        var spawnShape = PatternShapes.Rhombus(config.SpawnSize);

        var targets = new List<ITakenCell> { cell.AsTaken() };

        for (var i = 1; i < config.MaxChain; i++)
        {
            var next = SelectMine(targets[^1].Position);

            if (next == null)
                break;

            targets.Add(next);
        }

        var spawnedMines = new List<Position>();

        foreach (var mine in targets)
        {
            var candidates = spawnShape.SelectAll(board, mine.Position);

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

        board.OnUpdated();

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ChainReaction
            {
                TargetPlayer = board.OwnerId,
                SpawnedMines = spawnedMines,
                TargetCells = targets.Select(t => t.Position).ToList()
            }
        };

        ITakenCell? SelectMine(Position center)
        {
            return searchShape.SelectTaken(board, center)
                              .Where(x => x.HasMine && x.IsFlagged == false && targets.Contains(x) == false)
                              .OrderBy(x => x.Position.DistanceTo(center))
                              .FirstOrDefault();
        }
    }
}