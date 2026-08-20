using System.Collections.Generic;
using System.Reflection;
using Shared;

namespace Game.GamePlay.Snapshots;

public static class SnapshotApplier
{
    public static GameStateSnapshot Apply(GameStateSnapshot pre, SharedMoveSnapshot records)
    {
        var state = pre.Clone();

        foreach (var record in records.Records)
            ApplyRecord(state, record);

        return state;
    }

    private static void ApplyRecord(GameStateSnapshot state, IMoveSnapshotRecord record)
    {
        switch (record)
        {
            case GameStartedRecord:
                break;

            case SharedBoardSnapshot boardSnapshot:
                ApplyBoardSnapshot(state, boardSnapshot);
                break;

            case PlayerSnapshotRecord.CardUse cardUse:
                ApplyCardUseReveals(state, cardUse);
                break;

            case PlayerSnapshotRecord.CardAdd add:
                if (state.Players.TryGetValue(add.PlayerId, out var addPlayer) == true)
                {
                    if (add.IsStash == true)
                        addPlayer.Stash.Add(add.Type);
                    else
                        addPlayer.Hand[add.CardId] = add.Type;
                }

                break;

            case PlayerSnapshotRecord.CardRemove remove:
                if (state.Players.TryGetValue(remove.PlayerId, out var removePlayer) == true)
                    removePlayer.Hand.Remove(remove.CardId);
                break;
            case PlayerSnapshotRecord.ManaUpdate mana:
                if (state.Players.TryGetValue(mana.PlayerId, out var manaPlayer) == true)
                {
                    manaPlayer.ManaCurrent = mana.Current;
                    manaPlayer.ManaBaseMax = mana.BaseMax;
                    manaPlayer.ManaResultMax = mana.ResultMax;
                }

                break;

            case PlayerSnapshotRecord.HealthUpdate health:
                if (state.Players.TryGetValue(health.PlayerId, out var healthPlayer) == true)
                {
                    healthPlayer.HealthCurrent = health.Current;
                    healthPlayer.HealthBaseMax = health.BaseMax;
                    healthPlayer.HealthResultMax = health.ResultMax;
                }

                break;

            case PlayerSnapshotRecord.MovesUpdate moves:
                if (state.Players.TryGetValue(moves.PlayerId, out var movesPlayer) == true)
                {
                    movesPlayer.MovesLeft = moves.Left;
                    movesPlayer.MovesBaseMax = moves.BaseMax;
                    movesPlayer.MovesResultMax = moves.ResultMax;
                    movesPlayer.MovesIsAvailable = moves.IsAvailable;
                }

                break;
            case PlayerSnapshotRecord.ModifierUpdate modifier:
                if (state.Players.TryGetValue(modifier.PlayerId, out var modPlayer) == true)
                {
                    var list = modPlayer.Modifiers;
                    var existingIndex = list.FindIndex(o => o.SourceId == modifier.Overview.SourceId);

                    if (modifier.Overview.TurnsToEnd == 0)
                    {
                        if (existingIndex >= 0)
                            list.RemoveAt(existingIndex);
                    }
                    else if (existingIndex >= 0)
                    {
                        list[existingIndex] = modifier.Overview;
                    }
                    else
                    {
                        list.Add(modifier.Overview);
                    }
                }

                break;

            case PlayerSnapshotRecord.DeckUpdate:
            case PlayerSnapshotRecord.StashUpdate:
            case PlayerSnapshotRecord.BoardStateUpdate:
            case GameCompletedRecord:
            case TimeLimitedRoundRecord:
            case LastManStandingRoundRecord:
                break;
        }
    }

    private static void ApplyCardUseReveals(GameStateSnapshot state, PlayerSnapshotRecord.CardUse cardUse)
    {
        var data = cardUse.Data;

        if (data == null)
            return;

        var openedCells = GetOpenedCells(data);

        if (openedCells == null || openedCells.Count == 0)
            return;

        if (state.Boards.TryGetValue(data.TargetPlayer, out var board) == false)
            return;

        foreach (var opened in openedCells)
        {
            var hasMine = board.Cells.TryGetValue(opened.Position, out var existing) == true && existing.HasMine;

            board.Cells[opened.Position] = new CellStateSnapshot
            {
                Status = CellStatus.Free,
                MinesAround = opened.MinesAround,
                HasMine = hasMine
            };
        }
    }

    private static IReadOnlyList<OpenedCell>? GetOpenedCells(ICardActionData data)
    {
        var type = data.GetType();

        var updatedProp = type.GetProperty("UpdatedFreeCells", BindingFlags.Public | BindingFlags.Instance);
        if (updatedProp != null)
        {
            var value = updatedProp.GetValue(data) as IReadOnlyList<OpenedCell>;
            if (value != null && value.Count > 0)
                return value;
        }

        var openedProp = type.GetProperty("OpenedCells", BindingFlags.Public | BindingFlags.Instance);
        if (openedProp != null)
        {
            var value = openedProp.GetValue(data) as IReadOnlyList<OpenedCell>;
            if (value != null && value.Count > 0)
                return value;
        }

        return null;
    }

    private static void ApplyBoardSnapshot(GameStateSnapshot state, SharedBoardSnapshot snapshot)
    {
        if (state.Boards.TryGetValue(snapshot.BoardOwnerId, out var board) == false)
            return;

        foreach (var record in snapshot.Records)
            ApplyBoardRecord(board, record);
    }

    private static void ApplyBoardRecord(BoardStateSnapshot board, IBoardSnapshotRecord record)
    {
        switch (record)
        {
            case BoardSnapshotRecord.CellTaken taken:
                board.Cells[taken.Position] = new CellStateSnapshot { Status = CellStatus.Taken };
                break;

            case BoardSnapshotRecord.CellFree free:
                board.Cells[free.Position] = new CellStateSnapshot { Status = CellStatus.Free };
                break;

            case BoardSnapshotRecord.Flag flag:
                if (board.Cells.TryGetValue(flag.Position, out var flagCell) == true)
                    flagCell.IsFlagged = flag.IsFlagged;
                break;

            case BoardSnapshotRecord.MinesAround mines:
                if (board.Cells.TryGetValue(mines.Position, out var minesCell) == true)
                    minesCell.MinesAround = mines.Count;
                break;

            case BoardSnapshotRecord.Explosion:
                break;

            case BoardSnapshotRecord.EffectAdded added:
                if (board.Cells.TryGetValue(added.Position, out var addedCell) == true)
                    addedCell.Effects[added.EffectId] = added.Type;
                break;

            case BoardSnapshotRecord.EffectRemoved removed:
                if (board.Cells.TryGetValue(removed.Position, out var removedCell) == true)
                    removedCell.Effects.Remove(removed.EffectId);
                break;
        }
    }
}