using Shared;

namespace Game.GamePlay;

/// <summary>
/// A short summary of what the card did to the board, for the session log: after the match it shows
/// how many mines each side removed or placed with cards. Cards with no effect on mines
/// and cells produce no rows.
/// </summary>
public static class CardActionLog
{
    public static string? Describe(ICardActionData data)
    {
        return data switch
        {
            CardActionSnapshot.Bloodhound b => $"Defused={b.ExplodedCells.Count} Opened={b.OpenedCells.Count}",
            CardActionSnapshot.ErosionDozer e => $"Defused={e.ExplodedCells.Count} Opened={e.OpenedCells.Count}",
            CardActionSnapshot.ZipZap z => $"Defused={z.TargetCells.Count} Opened={z.OpenedCells.Count}",
            CardActionSnapshot.MinefieldScout m => $"Flagged={m.FlaggedCells.Count} Opened={m.OpenedCells.Count}",
            CardActionSnapshot.OpponentBomb o => $"Exploded={o.ExplodedCells.Count} Opened={o.OpenedCells.Count}",
            CardActionSnapshot.Trebuchet t => $"Closed={t.TakenCells.Count}",
            CardActionSnapshot.ChainReaction c => $"Closed={c.TakenCells.Count}",
            CardActionSnapshot.OpponentFlagErase f => $"Unflagged={f.UnflaggedCells.Count}",
            CardActionSnapshot.OpponentFlagReshuffle r => $"Flagged={r.FlaggedCells.Count} Unflagged={r.UnflaggedCells.Count}",
            _ => null
        };
    }
}
