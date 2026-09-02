using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Swaps a diamond area between the owner's and opponent's fields, transferring all cell states.
/// </summary>
public class DimensionRift : ICard<CardUsePayload.DimensionRift>
{
    public DimensionRift(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.DimensionRift payload)
    {
        var invoker = context.Invoker;
        var opponent = _gameContext.GetOpponent(invoker);
        var opponentBoard = opponent.Board;
        var ownerBoard = invoker.Board;
        opponentBoard.EnsureGenerated(context.Snapshot, payload.Position);
        ownerBoard.EnsureGenerated(context.Snapshot, payload.Position);

        var snapshot = context.Snapshot;
        var config = _configs.Value.DimensionRift_Normal;
        var pattern = PatternShapes.Rhombus(config.Size);

        // Обмен идёт по одинаковым координатам, поэтому берём только те позиции узора,
        // которые существуют на обеих досках.
        var positions = pattern.SelectAll(opponentBoard, payload.Position)
                               .Select(cell => cell.Position)
                               .Where(position => ownerBoard.Cells.ContainsKey(position))
                               .ToList();

        if (positions.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No cells in the pattern")
            };
        }

        var ownerStates = positions.Select(position => CellState.From(ownerBoard.Cells[position])).ToList();
        var opponentStates = positions.Select(position => CellState.From(opponentBoard.Cells[position])).ToList();

        for (var i = 0; i < positions.Count; i++)
        {
            Apply(ownerBoard, positions[i], opponentStates[i]);
            Apply(opponentBoard, positions[i], ownerStates[i]);
        }

        var ownerUpdated = ToOpenedCells(ownerBoard.MinesScanner.Recalculate(snapshot));
        var opponentUpdated = ToOpenedCells(opponentBoard.MinesScanner.Recalculate(snapshot));

        var ownerResult = Describe(ownerBoard, positions);
        var opponentResult = Describe(opponentBoard, positions);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.DimensionRift()
        {
            TargetPlayer = opponentBoard.OwnerId,
            OwnerPlayer = ownerBoard.OwnerId,
            TargetCells = positions,

            OwnerTakenCells = ownerResult.Taken,
            OwnerFlaggedCells = ownerResult.Flagged,
            OwnerUnflaggedCells = ownerResult.Unflagged,
            OwnerOpenedCells = ownerResult.Opened,
            OwnerUpdatedFreeCells = ownerUpdated,

            TargetTakenCells = opponentResult.Taken,
            TargetFlaggedCells = opponentResult.Flagged,
            TargetUnflaggedCells = opponentResult.Unflagged,
            TargetOpenedCells = opponentResult.Opened,
            TargetUpdatedFreeCells = opponentUpdated
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }

    /// <summary>
    /// Клетка пересоздаётся целиком: у <see cref="ITakenCell"/> нельзя снять мину,
    /// поэтому перенести чужое состояние можно только новым объектом.
    /// </summary>
    private static void Apply(IBoard board, Position position, CellState state)
    {
        // Эффекты (туман, заморозка) наложены на позицию, а не на её содержимое,
        // поэтому переезжают в новую клетку без изменений.
        var effects = board.Cells[position].Effects.ToList();

        if (state.IsTaken == false)
        {
            board.SetCell(Restore(new FreeCell(position, board), effects));
            return;
        }

        var taken = new TakenCell(position, board);
        board.SetCell(taken);

        if (state.HasMine == true)
            taken.SetMine();

        if (state.IsFlagged == true)
            taken.SetFlag();

        Restore(taken, effects);
    }

    private static TCell Restore<TCell>(TCell cell, IReadOnlyList<ICellEffect> effects) where TCell : ICell
    {
        foreach (var effect in effects)
            cell.AddEffect(effect);

        return cell;
    }

    private static BoardSwapResult Describe(IBoard board, IReadOnlyList<Position> positions)
    {
        var taken = new List<Position>();
        var flagged = new List<Position>();
        var unflagged = new List<Position>();
        var opened = new List<OpenedCell>();

        foreach (var position in positions)
        {
            var cell = board.Cells[position];

            if (cell.IsTaken() == false)
            {
                opened.Add(new OpenedCell
                {
                    Position = position,
                    MinesAround = cell.AsFree().MinesAround
                });

                continue;
            }

            taken.Add(position);

            if (cell.AsTaken().IsFlagged == true)
                flagged.Add(position);
            else
                unflagged.Add(position);
        }

        return new BoardSwapResult(taken, flagged, unflagged, opened);
    }

    private static List<OpenedCell> ToOpenedCells(IReadOnlyList<BoardSnapshotRecord.MinesAround> records)
    {
        return records.Select(record => new OpenedCell { Position = record.Position, MinesAround = record.Count })
                      .ToList();
    }

    private readonly record struct CellState(bool IsTaken, bool HasMine, bool IsFlagged)
    {
        public static CellState From(ICell cell)
        {
            if (cell.IsTaken() == false)
                return new CellState(false, false, false);

            var taken = cell.AsTaken();

            return new CellState(true, taken.HasMine, taken.IsFlagged);
        }
    }

    private readonly record struct BoardSwapResult(
        IReadOnlyList<Position> Taken,
        IReadOnlyList<Position> Flagged,
        IReadOnlyList<Position> Unflagged,
        IReadOnlyList<OpenedCell> Opened);
}
