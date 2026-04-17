using Shared;

namespace Game.GamePlay;

public class GameStateSnapshot
{
    public Dictionary<Guid, PlayerStateSnapshot> Players { get; set; } = new();
    public Dictionary<Guid, BoardStateSnapshot> Boards { get; set; } = new();

    public GameStateSnapshot Clone()
    {
        var clone = new GameStateSnapshot();

        foreach (var (id, player) in Players)
            clone.Players[id] = player.Clone();

        foreach (var (id, board) in Boards)
            clone.Boards[id] = board.Clone();

        return clone;
    }
}

public class PlayerStateSnapshot
{
    public int ManaCurrent { get; set; }
    public int ManaMax { get; set; }
    public int HealthCurrent { get; set; }
    public int HealthMax { get; set; }
    public int MovesLeft { get; set; }
    public int MovesMax { get; set; }
    public bool MovesIsAvailable { get; set; }
    public Dictionary<PlayerModifier, float> Modifiers { get; set; } = new();
    public Dictionary<Guid, CardType> Hand { get; set; } = new();
    public List<CardType> Stash { get; set; } = new();

    public PlayerStateSnapshot Clone()
    {
        return new PlayerStateSnapshot
        {
            ManaCurrent = ManaCurrent,
            ManaMax = ManaMax,
            HealthCurrent = HealthCurrent,
            HealthMax = HealthMax,
            MovesLeft = MovesLeft,
            MovesMax = MovesMax,
            MovesIsAvailable = MovesIsAvailable,
            Modifiers = new Dictionary<PlayerModifier, float>(Modifiers),
            Hand = new Dictionary<Guid, CardType>(Hand),
            Stash = new List<CardType>(Stash)
        };
    }
}

public class BoardStateSnapshot
{
    public Dictionary<Position, CellStateSnapshot> Cells { get; set; } = new();

    public BoardStateSnapshot Clone()
    {
        var clone = new BoardStateSnapshot();

        foreach (var (position, cell) in Cells)
            clone.Cells[position] = cell.Clone();

        return clone;
    }
}

public class CellStateSnapshot
{
    public CellStatus Status { get; set; }
    public bool IsFlagged { get; set; }
    public int MinesAround { get; set; }
    public bool HasMine { get; set; }
    public Dictionary<Guid, CellEffectType> Effects { get; set; } = new();

    public CellStateSnapshot Clone()
    {
        return new CellStateSnapshot
        {
            Status = Status,
            IsFlagged = IsFlagged,
            MinesAround = MinesAround,
            HasMine = HasMine,
            Effects = new Dictionary<Guid, CellEffectType>(Effects)
        };
    }
}
