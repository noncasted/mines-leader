using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class CardUseCommand(GameCommandUtils utils, ICardConfigs configs, MoveSnapshotAccessor snapshotAccessor)
    : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var player = context.Player;
        var handCard = player.Hand.Entries.FirstOrDefault(c => c.Id == request.CardId);

        if (handCard == null)
            return EmptyResponse.Fail($"Card {request.CardId} not found in hand");

        var config = configs.Value.All[handCard.Type];
        var manaCost = config.ManaCost;
        var nextDiscount = (int)player.Modifiers.Get(PlayerModifier.NextCardDiscount);
        var allDiscount = (int)player.Modifiers.Get(PlayerModifier.AllCardsDiscount);
        var penalty = (int)player.Modifiers.Get(PlayerModifier.ManaCostPenalty);

        if (nextDiscount > 0)
        {
            manaCost -= nextDiscount;
            player.Modifiers.Reset(PlayerModifier.NextCardDiscount);
        }

        manaCost -= allDiscount;
        manaCost += penalty;

        if (manaCost < 0)
            manaCost = 0;

        snapshotAccessor.Set(context.Snapshot, request.CardId);

        var takenBefore = SnapshotTakenCells();

        var use = Utils.ServiceProvider.Use(player, request.Payload);

        if (use.Result.HasError == true)
            return use.Result;

        player.Hand.Remove(request.CardId);

        if (use.ActionData != null)
        {
            AssignActionCells(use.ActionData, takenBefore);
            context.Snapshot.RecordCardUse(player.User.Id, request.CardId, use.ActionData);
        }

        foreach (var (_, board) in Utils.GameContext.Boards)
            board.OnUpdated();

        player.Stash.Add(handCard.Type);
        player.Mana.Use(manaCost);
        player.Moves.OnUsed();
        context.Player.Actions.OnCardUsed(handCard.Type, request.Payload);

        Utils.SessionLogger.LogCardUsed(player.User.Id, handCard.Type, manaCost, use.Result.HasError == false);

        return use.Result;
    }

    private Dictionary<Guid, HashSet<Position>> SnapshotTakenCells()
    {
        var result = new Dictionary<Guid, HashSet<Position>>();

        foreach (var (_, board) in Utils.GameContext.Boards)
        {
            var taken = new HashSet<Position>();

            foreach (var (position, cell) in board.Cells)
            {
                if (cell.Status == CellStatus.Taken)
                    taken.Add(position);
            }

            result[board.OwnerId] = taken;
        }

        return result;
    }

    private void AssignActionCells(ICardActionData data, Dictionary<Guid, HashSet<Position>> takenBefore)
    {
        if (takenBefore.TryGetValue(data.TargetPlayer, out var before) == false)
            return;

        IBoard? targetBoard = null;

        foreach (var (_, board) in Utils.GameContext.Boards)
        {
            if (board.OwnerId == data.TargetPlayer)
            {
                targetBoard = board;
                break;
            }
        }

        if (targetBoard == null)
            return;

        var opened = new List<Position>();

        foreach (var position in before)
        {
            if (targetBoard.Cells.TryGetValue(position, out var cell) == false)
                continue;

            if (cell.Status == CellStatus.Free)
                opened.Add(position);
        }

        if (opened.Count == 0)
            return;

        switch (data)
        {
            case CardActionSnapshot.ZipZap d: d.ActionCells = opened; break;
            case CardActionSnapshot.Bloodhound d: d.ActionCells = opened; break;
            case CardActionSnapshot.ErosionDozer d: d.ActionCells = opened; break;
            case CardActionSnapshot.Excavator d: d.ActionCells = opened; break;
            case CardActionSnapshot.ChaosDiamond d: d.ActionCells = opened; break;
            case CardActionSnapshot.ChaosScout d: d.ActionCells = opened; break;
            case CardActionSnapshot.OpponentBomb d: d.ActionCells = opened; break;
            default:
                Utils.SessionLogger.Log($"[CardUseCommand] ActionCells not assigned: type={data.GetType().Name}");
                return;
        }

        Utils.SessionLogger.Log(
            $"[CardUseCommand] ActionCells assigned: type={data.GetType().Name}, count={opened.Count}");
    }
}