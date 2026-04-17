using Cluster.Configs;
using Shared;

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

    public CardUseResult Use(CardUseContext context, CardUsePayload.ChainReaction payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.TryGetValue(payload.Position, out var cell) == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail($"No cell at position {payload.Position}")
            };
        }

        if (cell.IsTaken() == false || cell.AsTaken().HasMine == false)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("Target cell has no mine")
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
        var takenPositions = new List<Position>();

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
                        takenPositions.Add(candidate.Position);
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

        var minesRecords = board.MinesScanner.Recalculate(snapshot);

        var updatedFreeCells = minesRecords
                               .Select(r => new OpenedCell { Position = r.Position, MinesAround = r.Count })
                               .ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ChainReaction
        {
            TargetPlayer = board.OwnerId,
            SpawnedMines = spawnedMines,
            TargetCells = targets.Select(t => t.Position).ToList(),
            TakenCells = takenPositions,
            UpdatedFreeCells = updatedFreeCells
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
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