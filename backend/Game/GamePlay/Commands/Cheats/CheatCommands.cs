using Shared;

namespace Game.GamePlay;

public class CardAddCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.CardAdd>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.CardAdd request)
    {
        var activeCard = context.Player.Hand.Add(request.Type);
        context.Snapshot.RecordCardAdd(context.Player.User.Id, activeCard.Id, activeCard.Type);

        return EmptyResponse.Ok;
    }
}

public class CardDiscardCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.CardRemove>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.CardRemove request)
    {
        context.Player.Hand.Remove(request.CardId);
        context.Snapshot.RecordCardRemove(context.Player.User.Id, request.CardId);
        return EmptyResponse.Ok;
    }
}

public class ChangeManaCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.ChangeMana>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMana request)
    {
        var current = context.Player.Mana.Current;
        context.Player.Mana.SetCurrent(context.Snapshot, current + request.Value);
        return EmptyResponse.Ok;
    }
}

public class ChangeMaxManaCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.ChangeMaxMana>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMaxMana request)
    {
        var mana = context.Player.Mana;
        mana.SetMax(context.Snapshot, mana.BaseMax + request.Value);
        mana.Restore(context.Snapshot);
        return EmptyResponse.Ok;
    }
}

public class ChangeHealthCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.ChangeHealth>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeHealth request)
    {
        var current = context.Player.Health.Current;
        context.Player.Health.SetCurrent(context.Snapshot, current + request.Value);
        return EmptyResponse.Ok;
    }
}

public class ChangeMaxHealthCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.ChangeMaxHealth>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMaxHealth request)
    {
        var health = context.Player.Health;
        health.SetMax(context.Snapshot, health.BaseMax + request.Value);
        health.SetCurrent(context.Snapshot, health.ResultMax);
        return EmptyResponse.Ok;
    }
}

public class ChangeMovesCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.ChangeMoves>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMoves request)
    {
        var current = context.Player.Moves.Left;
        context.Player.Moves.SetCurrent(context.Snapshot, current + request.Value);
        return EmptyResponse.Ok;
    }
}

public class ChangeMaxMovesCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.ChangeMaxMoves>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMaxMoves request)
    {
        var moves = context.Player.Moves;
        moves.SetMax(context.Snapshot, moves.BaseMax + request.Value);
        moves.Restore(context.Snapshot);
        return EmptyResponse.Ok;
    }
}

public class SetMaxManaCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.SetMaxMana>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.SetMaxMana request)
    {
        context.Player.Mana.SetMax(context.Snapshot, request.Value);
        context.Player.Mana.Restore(context.Snapshot);
        return EmptyResponse.Ok;
    }
}

public class SetMaxHealthCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.SetMaxHealth>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.SetMaxHealth request)
    {
        context.Player.Health.SetMax(context.Snapshot, request.Value);
        context.Player.Health.SetCurrent(context.Snapshot, request.Value);
        return EmptyResponse.Ok;
    }
}

public class SetMaxMovesCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.SetMaxMoves>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.SetMaxMoves request)
    {
        context.Player.Moves.SetMax(context.Snapshot, request.Value);
        context.Player.Moves.Restore(context.Snapshot);
        return EmptyResponse.Ok;
    }
}

public class RestoreManaCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.RestoreMana>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.RestoreMana request)
    {
        context.Player.Mana.Restore(context.Snapshot);
        return EmptyResponse.Ok;
    }
}

public class RestoreHealthCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.RestoreHealth>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.RestoreHealth request)
    {
        context.Player.Health.SetCurrent(context.Snapshot, context.Player.Health.ResultMax);
        return EmptyResponse.Ok;
    }
}

public class RestoreMovesCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.RestoreMoves>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.RestoreMoves request)
    {
        context.Player.Moves.Restore(context.Snapshot);
        return EmptyResponse.Ok;
    }
}

public class EndMatchCheat(GameCommandUtils utils, ICheatsAccess access) : CheatCommand<GameCheatContexts.EndMatch>(utils, access)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.EndMatch request)
    {
        var winner = Utils.GameContext.Players.First(t => t.User.Id == request.Winner);
        var loser = Utils.GameContext.GetOpponent(winner);
        loser.Health.SetCurrent(context.Snapshot, -1000);
        Utils.GameRound.SkipTurn();
        return EmptyResponse.Ok;
    }
}