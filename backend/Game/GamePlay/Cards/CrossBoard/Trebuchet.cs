using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Trebuchet : ICard<CardUsePayload.Trebuchet>
{
    public Trebuchet(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Trebuchet payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells")
            };
        }

        var config = _configs.Value.Trebuchet_Normal;
        var size = config.Size + (int)invoker.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern")
            };
        }

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

        var takenPositions = selected.Select(c => c.Position).ToList();
        var minesRecords = board.MinesScanner.Recalculate(snapshot);
        var updatedFreeCells = minesRecords
            .Select(r => new OpenedCell { Position = r.Position, MinesAround = r.Count })
            .ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Trebuchet()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = takenPositions,
            TakenCells = takenPositions,
            UpdatedFreeCells = updatedFreeCells
        });

        invoker.Modifiers.Reset(snapshot, PlayerModifier.TrebuchetBoost);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}