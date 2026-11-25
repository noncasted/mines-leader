using Meta.Matches;
using Meta.Users;
using Shared;

namespace Game.GamePlay;

public partial class MoveSnapshot
{
    public void RecordGameCompleted(
        Guid winner,
        IReadOnlyList<Guid> players,
        IReadOnlyDictionary<Guid, UserStatsDelta> stats,
        MatchCompletionSummary? summary,
        IEnumerable<IBoard> boards)
    {
        Append(new GameCompletedRecord
        {
            Winner = winner,
            Duration = summary?.Duration ?? TimeSpan.Zero,
            Players = players.Select(id => CreatePlayerResult(id, stats.GetValueOrDefault(id), summary))
                             .ToList(),
            Boards = boards.Select(CreateBoardReveal).ToList()
        });
    }

    private static BoardRevealState CreateBoardReveal(IBoard board)
    {
        var cells = new List<CellRevealState>(board.Cells.Count);

        foreach (var cell in board.Cells.Values)
        {
            var state = new CellRevealState { Position = cell.Position };

            switch (cell)
            {
                case IFreeCell free:
                    state.IsFree = true;
                    state.MinesAround = free.MinesAround;
                    break;
                case ITakenCell taken:
                    state.IsFlagged = taken.IsFlagged;
                    state.HasMine = taken.HasMine;
                    break;
            }

            cells.Add(state);
        }

        return new BoardRevealState
        {
            OwnerId = board.OwnerId,
            Cells = cells
        };
    }

    private static MatchPlayerResult CreatePlayerResult(
        Guid id,
        UserStatsDelta? delta,
        MatchCompletionSummary? summary)
    {
        delta ??= new UserStatsDelta();

        return new MatchPlayerResult
        {
            PlayerId = id,
            RatingChange = summary?.RatingChanges.GetValueOrDefault(id) ?? 0,
            Rating = summary?.Ratings.GetValueOrDefault(id) ?? 0,
            Stats = new MatchPlayerStats
            {
                Counters = new Dictionary<UserStatType, long>(delta.Counters),
                CardsPlayedByGroup = new Dictionary<CardGroup, long>(delta.CardsPlayedByGroup)
            }
        };
    }
}
