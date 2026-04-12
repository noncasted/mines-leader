using Shared;

namespace Game.GamePlay;

public class CardAddCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.CardAdd>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.CardAdd request)
    {
        var activeCard = context.Player.Hand.Add(request.Type);
        context.Snapshot.RecordCardAdd(context.Player.User.Id, activeCard.Id, activeCard.Type);

        return EmptyResponse.Ok;
    }
}

public class CardDiscardCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.CardRemove>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.CardRemove request)
    {
        context.Player.Hand.Remove(request.CardId);
        return EmptyResponse.Ok;
    }
}

public class ChangeManaCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.ChangeMana>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMana request)
    {
        var current = context.Player.Mana.Current;
        context.Player.Mana.SetCurrent(current + request.Value);
        return EmptyResponse.Ok;
    }
}

public class ChangeMaxManaCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.ChangeMaxMana>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMaxMana request)
    {
        var mana = context.Player.Mana;
        mana.SetMax(mana.Max + request.Value);
        mana.Restore();
        return EmptyResponse.Ok;
    }
}

public class ChangeHealthCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.ChangeHealth>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeHealth request)
    {
        var current = context.Player.Health.Current.Value;
        context.Player.Health.SetCurrent(current + request.Value);
        return EmptyResponse.Ok;
    }
}

public class ChangeMaxHealthCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.ChangeMaxHealth>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMaxHealth request)
    {
        var health = context.Player.Health;
        health.SetMax(health.Max + request.Value);
        health.SetCurrent(health.Max);
        return EmptyResponse.Ok;
    }
}

public class ChangeMovesCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.ChangeMoves>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMoves request)
    {
        var current = context.Player.Moves.Left;
        context.Player.Moves.SetCurrent(current + request.Value);
        return EmptyResponse.Ok;
    }
}

public class ChangeMaxMovesCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.ChangeMaxMoves>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.ChangeMaxMoves request)
    {
        var moves = context.Player.Moves;
        moves.SetMax(moves.Max + request.Value);
        moves.Restore();
        return EmptyResponse.Ok;
    }
}

public class SetMaxManaCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.SetMaxMana>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.SetMaxMana request)
    {
        context.Player.Mana.SetMax(request.Value);
        context.Player.Mana.Restore();
        return EmptyResponse.Ok;
    }
}

public class SetMaxHealthCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.SetMaxHealth>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.SetMaxHealth request)
    {
        context.Player.Health.SetMax(request.Value);
        context.Player.Health.SetCurrent(request.Value);
        return EmptyResponse.Ok;
    }
}

public class SetMaxMovesCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.SetMaxMoves>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.SetMaxMoves request)
    {
        context.Player.Moves.SetMax(request.Value);
        context.Player.Moves.Restore();
        return EmptyResponse.Ok;
    }
}

public class RestoreManaCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.RestoreMana>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.RestoreMana request)
    {
        context.Player.Mana.Restore();
        return EmptyResponse.Ok;
    }
}

public class RestoreHealthCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.RestoreHealth>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.RestoreHealth request)
    {
        context.Player.Health.SetCurrent(context.Player.Health.Max);
        return EmptyResponse.Ok;
    }
}

public class RestoreMovesCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.RestoreMoves>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.RestoreMoves request)
    {
        context.Player.Moves.Restore();
        return EmptyResponse.Ok;
    }
}

public class EndMatchCheat(GameCommandUtils utils) : GameCommand<GameCheatContexts.EndMatch>(utils)
{
    protected override EmptyResponse Execute(Context context, GameCheatContexts.EndMatch request)
    {
        var winner = Utils.GameContext.Players.First(t => t.User.Id == request.Winner);
        var loser = Utils.GameContext.GetOpponent(winner);
        loser.Health.SetCurrent(-1000);
        Utils.GameRound.SkipTurn();
        return EmptyResponse.Ok;
    }
}