using Shared;

namespace Game.GamePlay.Snapshots;

public static class GameStateCapture
{
    public static GameStateSnapshot Capture(IGameContext context)
    {
        var snapshot = new GameStateSnapshot();

        foreach (var player in context.Players)
            snapshot.Players[player.User.Id] = CapturePlayer(player);

        foreach (var (_, board) in context.Boards)
            snapshot.Boards[board.OwnerId] = CaptureBoard(board);

        return snapshot;
    }

    private static PlayerStateSnapshot CapturePlayer(IPlayer player)
    {
        var state = new PlayerStateSnapshot
        {
            ManaCurrent = player.Mana.Current,
            ManaBaseMax = player.Mana.BaseMax,
            ManaResultMax = player.Mana.ResultMax,
            HealthCurrent = player.Health.Current,
            HealthBaseMax = player.Health.BaseMax,
            HealthResultMax = player.Health.ResultMax,
            MovesLeft = player.Moves.Left,
            MovesBaseMax = player.Moves.BaseMax,
            MovesResultMax = player.Moves.ResultMax,
            MovesIsAvailable = player.Moves.IsAvailable,
            Modifiers = player.Modifiers.Sources.Select(s => s.GetOverview()).ToList()
        };

        foreach (var activeCard in player.Hand.Entries)
            state.Hand[activeCard.Id] = activeCard.Type;

        foreach (var stashedCard in player.Stash.Entries)
            state.Stash.Add(stashedCard);

        return state;
    }

    private static BoardStateSnapshot CaptureBoard(IBoard board)
    {
        var state = new BoardStateSnapshot();

        foreach (var (position, cell) in board.Cells)
            state.Cells[position] = CaptureCell(cell);

        return state;
    }

    private static CellStateSnapshot CaptureCell(ICell cell)
    {
        var state = new CellStateSnapshot
        {
            Status = cell.Status
        };

        if (cell.Status == CellStatus.Free)
        {
            state.MinesAround = cell.AsFree().MinesAround;
        }
        else
        {
            var taken = cell.AsTaken();
            state.IsFlagged = taken.IsFlagged;
            state.HasMine = taken.HasMine;
        }

        foreach (var effect in cell.Effects)
            state.Effects[effect.Id] = effect.Type;

        return state;
    }
}
