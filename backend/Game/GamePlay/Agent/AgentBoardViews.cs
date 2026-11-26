using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public static class AgentBoardViews
{
    public static void Fill(AgentPlayerView view, IBoard? board, bool oracle)
    {
        view.Cells = new List<AgentCellView>();
        view.BoardAscii = string.Empty;
        view.Mines = 0;
        view.Flags = 0;

        if (board == null)
            return;

        var width = board.Size.x;
        var height = board.Size.y;
        var lines = new List<string>(height);

        for (var y = 0; y < height; y++)
        {
            var chars = new char[width];

            for (var x = 0; x < width; x++)
            {
                var position = new Position(x, y);
                if (board.Cells.TryGetValue(position, out var cell) == false)
                {
                    chars[x] = '.';
                    continue;
                }

                var cellView = CreateCellView(cell, oracle);
                view.Cells.Add(cellView);
                chars[x] = ToAscii(cellView);
            }

            lines.Add(new string(chars));
        }

        view.BoardAscii = string.Join("\n", lines);

        if (board.MinesScanner != null)
        {
            view.Mines = board.MinesScanner.Mines + board.DetonatedMines;
            view.Flags = board.MinesScanner.Flags;
            return;
        }

        foreach (var cellView in view.Cells)
        {
            if (cellView.Status == "flagged")
                view.Flags++;

            if (cellView.HasMine || cellView.Status == "exploded")
                view.Mines++;
        }

        view.Mines += board.DetonatedMines;
    }

    private static AgentCellView CreateCellView(ICell cell, bool oracle)
    {
        var effects = new List<string>();
        foreach (var effect in cell.Effects)
            effects.Add(effect.Type.ToString());

        var view = new AgentCellView
        {
            X = cell.Position.x,
            Y = cell.Position.y,
            Effects = effects,
            Status = "closed"
        };

        if (cell.Status == CellStatus.Free)
        {
            view.Status = "open";
            view.MinesAround = cell.AsFree().MinesAround;
            return view;
        }

        var taken = cell.AsTaken();
        if (taken.IsFlagged)
        {
            view.Status = "flagged";
        }
        else
        {
            view.Status = "closed";
        }

        if (oracle || view.Status == "exploded")
            view.HasMine = taken.HasMine;

        return view;
    }

    private static char ToAscii(AgentCellView cell)
    {
        if (cell.Status == "exploded")
            return '*';

        if (cell.Status == "open")
            return (char)('0' + Math.Clamp(cell.MinesAround, 0, 8));

        if (cell.Status == "flagged")
            return 'F';

        foreach (var effect in cell.Effects)
        {
            if (effect == nameof(CellEffectType.Fog) || effect == nameof(CellEffectType.Blackout))
                return '~';
        }

        return '.';
    }
}
