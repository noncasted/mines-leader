using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public abstract class BotProfileBase : IBotProfileStrategy
{
    protected BotProfileBase(
        IBotConfig config,
        IBotContext botContext,
        IBotCellAction cellAction,
        IBotCardAction cardAction,
        IBotFlagAction flagAction,
        ISessionLogger sessionLogger,
        IGameRound round)
    {
        _config = config;
        _botContext = botContext;
        _cellAction = cellAction;
        _cardAction = cardAction;
        _flagAction = flagAction;
        _sessionLogger = sessionLogger;
        _round = round;
    }

    protected readonly IBotConfig _config;
    protected readonly IBotContext _botContext;
    protected readonly ISessionLogger _sessionLogger;
    protected readonly IGameRound _round;

    protected readonly IBotCellAction _cellAction;
    protected readonly IBotCardAction _cardAction;
    protected readonly IBotFlagAction _flagAction;

    public abstract BotProfile Profile { get; }
    public abstract int ConstraintDepth { get; }

    public abstract Task ExecuteTurn(IReadOnlyLifetime lifetime);

    protected void LogTurnStart()
    {
        var bot = _botContext.Bot;
        var handCards = string.Join(", ", bot.Hand.Entries.Select(c => c.Type));

        _sessionLogger.LogBotAction("State",
            $"Mana={bot.Mana.Current}/{bot.Mana.Max} Moves={bot.Moves.Left}/{bot.Moves.Max} Hand=[{handCards}] Health={bot.Health.Current.Value}");
    }

    protected async Task OpenFirstCell(IReadOnlyLifetime lifetime, float delayBefore, float delayAfter)
    {
        _sessionLogger.LogBotAction("Init", "Board empty, opening first cell");
        await Delay(delayBefore, lifetime);
        _cellAction.TryExecute();
        await Delay(delayAfter, lifetime);
    }

    protected async Task RunFlagPhase(int limit, DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var flagsPlaced = 0;

        while (flagsPlaced < limit)
        {
            var placed = _flagAction.TryExecute();

            if (placed == false)
                break;

            flagsPlaced++;
            await DelayForAction(startTime, roundTime, lifetime);
        }

        if (flagsPlaced > 0)
            _sessionLogger.LogBotAction("Flags", $"Placed {flagsPlaced} flags");
    }

    protected async Task RunCardPhase(int limit, DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var cardsUsed = 0;

        while (cardsUsed < limit && bot.Moves.Left > 0)
        {
            var usedCard = _cardAction.TryExecute(lifetime);

            if (usedCard == false)
                break;

            cardsUsed++;
            await DelayForAction(startTime, roundTime, lifetime);
        }

        if (cardsUsed > 0)
            _sessionLogger.LogBotAction("Cards", $"Used {cardsUsed} cards");
    }

    protected async Task RunCellPhase(int limit, DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var cellsOpened = 0;

        while (cellsOpened < limit && _botContext.Bot.Moves.Left > 0)
        {
            var opened = _cellAction.TryExecute();

            if (opened == false)
                break;

            cellsOpened++;
            await DelayForAction(startTime, roundTime, lifetime);
        }

        if (cellsOpened > 0)
            _sessionLogger.LogBotAction("Cells", $"Opened {cellsOpened} cells");
    }

    protected void EndTurn(DateTime startTime, float roundTime)
    {
        var bot = _botContext.Bot;
        var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;

        _sessionLogger.LogBotAction("EndTurn",
            $"MovesLeft={bot.Moves.Left} Time={elapsed:F1}s/{roundTime:F1}s");
        _round.SkipTurn();
    }

    protected async Task WaitRemainingTime(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
        var remaining = roundTime - elapsed;

        if (remaining > 0.5f)
            await Delay(remaining, lifetime);
    }

    protected static async Task DelayForAction(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
        var remaining = roundTime - elapsed;

        if (remaining <= 0.5f)
            return;

        var delay = 0.5f + (float)Random.Shared.NextDouble() * 1.5f;
        delay = Math.Min(delay, remaining * 0.4f);
        delay = Math.Max(delay, 0.3f);

        await Delay(delay, lifetime);
    }

    protected static Task Delay(float seconds, IReadOnlyLifetime lifetime)
    {
        return Task.Delay(TimeSpan.FromSeconds(seconds), lifetime.Token);
    }
}
