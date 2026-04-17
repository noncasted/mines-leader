using Microsoft.Extensions.Options;
using Shared;

namespace Game.GamePlay.Boards;

/// <summary>
/// Runtime-safe adaptation of <c>Tests.Game.BoardParser</c>.
/// Parses the same visual DSL ("t m _ x f g") but builds a production <see cref="Board"/>
/// without relying on <c>TestBoardBuilder</c> / NSubstitute.
///
/// Characters:
///   t — Taken (default)
///   m — Taken with mine
///   f — Taken flagged mine
///   g — Taken flagged no mine
///   _ — Free
///   x — Taken marker for the card target position
/// </summary>
public static class BoardLayoutParser
{
    public static (IBoard Board, Position Target) Parse(string layout, Guid? ownerId = null)
    {
        var rows = ParseRows(layout);

        if (rows.Count == 0)
            throw new ArgumentException("Layout is empty", nameof(layout));

        var height = rows.Count;
        var width = rows.Max(r => r.Length);
        var size = Math.Max(width, height);

        var mines = new HashSet<Position>();
        var freeCells = new HashSet<Position>();
        var flaggedCells = new HashSet<Position>();
        var target = new Position(-1, -1);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var ch = rows[y][x];
                var pos = new Position(x, y);

                switch (ch)
                {
                    case 't':
                        break;
                    case 'm':
                        mines.Add(pos);
                        break;
                    case 'f':
                        mines.Add(pos);
                        flaggedCells.Add(pos);
                        break;
                    case 'g':
                        flaggedCells.Add(pos);
                        break;
                    case '_':
                        freeCells.Add(pos);
                        break;
                    case 'x':
                        target = pos;
                        break;
                    default:
                        throw new ArgumentException($"Unknown layout char '{ch}' at ({x},{y})");
                }
            }
        }

        var options = Options.Create(new BoardOptions { Size = size, Mines = mines.Count });
        var board = new Board(ownerId ?? Guid.NewGuid(), options);

        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                var pos = new Position(x, y);
                var cell = new TakenCell(pos, board);

                if (mines.Contains(pos))
                    cell.SetMine();

                board.SetCell(cell);
            }
        }

        foreach (var pos in freeCells)
        {
            if (board.Cells.TryGetValue(pos, out var cell) && cell is ITakenCell taken)
                taken.ToFree();
        }

        foreach (var pos in flaggedCells)
        {
            if (board.Cells.TryGetValue(pos, out var cell) && cell is ITakenCell taken)
                taken.SetFlag();
        }

        board.MinesScanner.Recalculate();
        board.MinesScanner.Recalculate();

        return (board, target);
    }

    /// <summary>
    /// Capture the current board state as a compact <see cref="BoardLayoutSnapshot"/>.
    /// Used by the preview generator to ship the pre-action state to clients.
    /// </summary>
    public static BoardLayoutSnapshot Capture(IBoard board)
    {
        var cells = new List<PreviewCell>(board.Cells.Count);

        foreach (var pair in board.Cells)
        {
            var minesAround = pair.Value.Status == CellStatus.Free
                ? pair.Value.AsFree().MinesAround
                : 0;

            cells.Add(new PreviewCell
            {
                Position = pair.Key,
                Kind = Classify(pair.Value),
                MinesAround = minesAround
            });
        }

        return new BoardLayoutSnapshot
        {
            Size = board.Size.x,
            Cells = cells
        };
    }

    private static PreviewCellKind Classify(ICell cell)
    {
        if (cell.Status == CellStatus.Free)
            return PreviewCellKind.Free;

        var taken = (ITakenCell)cell;

        if (taken.IsFlagged && taken.HasMine)
            return PreviewCellKind.TakenFlagMine;

        if (taken.IsFlagged)
            return PreviewCellKind.TakenFlag;

        if (taken.HasMine)
            return PreviewCellKind.TakenMine;

        return PreviewCellKind.Taken;
    }

    private static List<char[]> ParseRows(string layout)
    {
        return layout
               .Split('\n', StringSplitOptions.RemoveEmptyEntries)
               .Select(line => line.Trim())
               .Where(line => line.Length > 0)
               .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(token => token[0])
                                   .ToArray())
               .ToList();
    }
}