using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay.Profiles;

public abstract class BotProfileBase : IBotProfileStrategy
{
    protected BotProfileBase(
        IBotConfig config,
        IBotContext botContext,
        IBotCellAction cellAction,
        IBotCardAction cardAction,
        IBotFlagAction flagAction,
        ISessionLogger sessionLogger,
        IGameRound round,
        MatchCreateOptions matchOptions)
    {
        _config = config;
        _botContext = botContext;
        _cellAction = cellAction;
        _cardAction = cardAction;
        _flagAction = flagAction;
        _sessionLogger = sessionLogger;
        _round = round;
        _matchOptions = matchOptions;
    }

    protected readonly IBotConfig _config;
    protected readonly IBotContext _botContext;
    protected readonly ISessionLogger _sessionLogger;
    protected readonly IGameRound _round;
    protected readonly MatchCreateOptions _matchOptions;

    protected readonly IBotCellAction _cellAction;
    protected readonly IBotCardAction _cardAction;
    protected readonly IBotFlagAction _flagAction;

    public abstract BotProfile Profile { get; }
    public abstract int ConstraintDepth { get; }

    public abstract Task ExecuteTurn(IReadOnlyLifetime lifetime);

    protected BotProfileConfig ProfileConfig => MatchBotProfile.ResolveConfig(_matchOptions, _config);

    protected void LogTurnStart()
    {
        var bot = _botContext.Bot;
        var handCards = string.Join(", ", bot.Hand.Entries.Select(c => c.Type));

        _sessionLogger.LogBotAction("State",
            $"Mana={bot.Mana.Current}/{bot.Mana.ResultMax} Moves={bot.Moves.Left}/{bot.Moves.ResultMax} Hand=[{handCards}] Health={bot.Health.Current}/{bot.Health.ResultMax}");
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
            await DelayForCard(startTime, roundTime, lifetime);
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

    /// <summary>
    /// Пауза перед завершением хода, чтобы игрок успел увидеть последние действия бота.
    /// Раунд может закончиться раньше — тогда ожидание обрывается вместе с lifetime.
    /// </summary>
    protected async Task WaitBeforeEndTurn(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var wait = Math.Max(0f, ProfileConfig.EndTurnDelay);

        if (BotTurnTiming.ShouldSkipRoundPadding(_matchOptions.Type) == false)
        {
            var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            wait = Math.Max(wait, roundTime - elapsed);
        }

        if (wait > 0.5f)
            await Delay(wait, lifetime);
    }

    /// <summary>
    /// Пауза между картами: разыгранная карта должна повисеть на столе,
    /// а не улететь в стеш одновременно со следующей.
    /// </summary>
    protected async Task DelayForCard(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var delay = Math.Max(0f, ProfileConfig.CardPlayDelay);

        if (delay <= 0f)
            return;

        if (BotTurnTiming.IsTurnBased(_matchOptions.Type) == false)
        {
            var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var remaining = roundTime - elapsed;

            if (remaining <= 0.5f)
                return;

            delay = Math.Min(delay, remaining);
        }

        await Delay(delay, lifetime);
    }

    protected async Task DelayForAction(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        if (BotTurnTiming.IsTurnBased(_matchOptions.Type))
        {
            var delay = ProfileConfig.ActionDelay;
            if (delay <= 0f)
                delay = 0.3f;

            await Delay(delay, lifetime);
            return;
        }

        var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
        var remaining = roundTime - elapsed;

        if (remaining <= 0.5f)
            return;

        var paced = 0.5f + (float)Random.Shared.NextDouble() * 1.5f;
        paced = Math.Min(paced, remaining * 0.4f);
        paced = Math.Max(paced, 0.3f);

        await Delay(paced, lifetime);
    }

    protected Task Delay(float seconds, IReadOnlyLifetime lifetime)
    {
        return Task.Delay(TimeSpan.FromSeconds(seconds), lifetime.Token);
    }
}
