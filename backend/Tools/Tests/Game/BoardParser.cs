using Game.GamePlay;
using Shared;

namespace Tests.Game;

/// <summary>
/// Parses visual board representations for deterministic card testing.
///
/// Initial state characters:
///   t — Taken cell (no mine)
///   m — Taken cell with mine
///   f — Taken cell with mine, flagged
///   g — Taken cell without mine, flagged
///   _ — Free cell
///   x — Taken cell, marks card target position
///
/// Expected state characters (after card use):
///   t — Taken, no mine, no flag
///   m — Taken, has mine, no flag
///   f — Taken, flagged (has mine)
///   g — Taken, flagged (no mine)
///   _ — Free (was already free)
///   R — Free (was Taken, revealed by card)
///   D — Free (was mine, defused by card)
///   E — Taken, has mine (exploded — stays Taken, event fired)
///   S — Taken with Smoke effect
///   F — Taken with Fog effect
///   * — skip (don't assert this cell)
/// </summary>
public static class BoardParser
{
    /// <summary>
    /// Parse a board string into an IBoard and extract the target position (x marker).
    /// </summary>
    public static (IBoard Board, Position Target) Parse(string layout)
    {
        var rows = ParseRows(layout);
        var height = rows.Count;
        var width = rows.Max(r => r.Length);

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
                        break; // Taken, default
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
                        throw new ArgumentException($"Unknown initial char '{ch}' at ({x},{y})");
                }
            }
        }

        var builder = new TestBoardBuilder(Math.Max(width, height));

        if (mines.Count > 0)
            builder.WithMinesAt(mines.Select(p => (p.x, p.y)).ToArray());

        if (freeCells.Count > 0)
            builder.WithFreeAt(freeCells.Select(p => (p.x, p.y)).ToArray());

        if (flaggedCells.Count > 0)
            builder.WithFlagAt(flaggedCells.Select(p => (p.x, p.y)).ToArray());

        return (builder.Build(), target);
    }

    /// <summary>
    /// Assert the board matches the expected layout.
    /// Throws with a visual diff on mismatch.
    /// </summary>
    public static void AssertBoard(IBoard board, string expectedLayout)
    {
        var rows = ParseRows(expectedLayout);
        var errors = new List<string>();

        for (var y = 0; y < rows.Count; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var ch = rows[y][x];
                var pos = new Position(x, y);

                if (ch == '*')
                    continue;

                if (!board.Cells.TryGetValue(pos, out var cell))
                {
                    errors.Add($"({x},{y}): expected '{ch}' but cell does not exist");
                    continue;
                }

                var error = AssertCell(cell, ch, pos);

                if (error != null)
                    errors.Add(error);
            }
        }

        if (errors.Count > 0)
        {
            var actual = Render(board, rows.Count, rows.Max(r => r.Length));

            throw new Exception($"Board mismatch ({errors.Count} cells differ):\n" +
                                $"\n  Expected:\n{Indent(expectedLayout)}\n" +
                                $"\n  Actual:\n{Indent(actual)}\n" +
                                $"\n  Errors:\n    {string.Join("\n    ", errors)}");
        }
    }

    private static string? AssertCell(ICell cell, char expected, Position pos)
    {
        var actual = CellToChar(cell);

        return expected switch
        {
            't' => cell.Status != CellStatus.Taken || (cell is ITakenCell t1 && (t1.HasMine || t1.IsFlagged))
                ? $"({pos.x},{pos.y}): expected 't' (Taken, clean) but got '{actual}'"
                : null,

            'm' => cell.Status != CellStatus.Taken || cell is not ITakenCell t2 || !t2.HasMine || t2.IsFlagged
                ? $"({pos.x},{pos.y}): expected 'm' (mine) but got '{actual}'"
                : null,

            'f' => cell.Status != CellStatus.Taken || cell is not ITakenCell t3 || !t3.IsFlagged || !t3.HasMine
                ? $"({pos.x},{pos.y}): expected 'f' (flagged mine) but got '{actual}'"
                : null,

            'g' => cell.Status != CellStatus.Taken || cell is not ITakenCell t4 || !t4.IsFlagged || t4.HasMine
                ? $"({pos.x},{pos.y}): expected 'g' (flagged, no mine) but got '{actual}'"
                : null,

            '_' or 'R' or 'D' => cell.Status != CellStatus.Free
                ? $"({pos.x},{pos.y}): expected Free ('{expected}') but got '{actual}'"
                : null,

            'E' => cell.Status != CellStatus.Taken || cell is not ITakenCell t5 || !t5.HasMine
                ? $"({pos.x},{pos.y}): expected 'E' (exploded mine, still Taken) but got '{actual}'"
                : null,

            _ => $"({pos.x},{pos.y}): unknown expected char '{expected}'"
        };
    }

    private static char CellToChar(ICell cell)
    {
        if (cell.Status == CellStatus.Free)
            return '_';

        var taken = (ITakenCell)cell;

        if (taken.IsFlagged && taken.HasMine)
            return 'f';

        if (taken.IsFlagged)
            return 'g';

        if (taken.HasMine)
            return 'm';

        return 't';
    }

    /// <summary>
    /// Render the current board state as a visual string.
    /// </summary>
    public static string Render(IBoard board, int height, int width)
    {
        var lines = new List<string>();

        for (var y = 0; y < height; y++)
        {
            var chars = new List<char>();

            for (var x = 0; x < width; x++)
            {
                var pos = new Position(x, y);
                chars.Add(board.Cells.TryGetValue(pos, out var cell) ? CellToChar(cell) : '?');
            }

            lines.Add(string.Join(' ', chars));
        }

        return string.Join('\n', lines);
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

    private static string Indent(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('\n', lines.Select(l => "    " + l.Trim()));
    }
}