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

    /// <summary>
    /// Лимит флагов на ход. Один бюджет живёт весь ход, чтобы проходы между открытиями
    /// клеток суммарно не превышали FlagsPerRound профиля.
    /// </summary>
    protected sealed class FlagBudget
    {
        public FlagBudget(int limit)
        {
            Left = limit;
        }

        public int Left { get; private set; }

        public void Use()
        {
            Left--;
        }
    }

    protected async Task<int> RunFlagPhase(FlagBudget budget, DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var flagsPlaced = 0;

        while (budget.Left > 0)
        {
            var placed = _flagAction.TryExecute();

            if (placed == false)
                break;

            budget.Use();
            flagsPlaced++;
            await DelayForAction(startTime, roundTime, lifetime);
        }

        if (flagsPlaced > 0)
            _sessionLogger.LogBotAction("Flags", $"Placed {flagsPlaced} flags");

        return flagsPlaced;
    }

    protected async Task RunCardPhase(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var cardsUsed = 0;

        // Лимита на количество карт нет: бот играет, пока есть подходящие карты, мана и ходы.
        while (bot.Moves.Left > 0)
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

    /// <summary>
    /// Цикл «флаги, открытие, флаги»: каждая открытая клетка даёт новые цифры, по ним сразу
    /// ставятся флаги, а флаги открывают аккорды. Первый проход флагов подхватывает клетки,
    /// открытые картами. Цикл идёт, пока есть ходы и логически выводимые клетки.
    /// </summary>
    protected async Task RunSolveLoop(FlagBudget budget, DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var cellsOpened = 0;
        var flagsPlaced = 0;

        while (true)
        {
            flagsPlaced += await RunFlagPhase(budget, startTime, roundTime, lifetime);

            if (bot.Moves.Left <= 0)
                break;

            var opened = _cellAction.TryExecute();

            if (opened == false)
                break;

            cellsOpened++;
            await DelayForAction(startTime, roundTime, lifetime);
        }

        _sessionLogger.LogBotAction("Solve",
            $"Opened {cellsOpened} cells, placed {flagsPlaced} flags between opens | MovesLeft={bot.Moves.Left} FlagBudgetLeft={budget.Left}");
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
    /// а не улететь в стеш одновременно со следующей. Когда бюджет раунда выбран,
    /// пауза сжимается до ActionDelay, но не до нуля.
    /// </summary>
    protected async Task DelayForCard(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var delay = Math.Max(0f, ProfileConfig.CardPlayDelay);

        if (BotTurnTiming.IsTurnBased(_matchOptions.Type) == false)
        {
            var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var remaining = Math.Max(0f, roundTime - elapsed);

            delay = Math.Max(Math.Min(delay, remaining), ActionDelay);
        }

        if (delay > 0f)
            await Delay(delay, lifetime);
    }

    /// <summary>
    /// Пауза между флагами и открытиями. Пока бюджет раунда не выбран, темп плавает,
    /// чтобы бот не выглядел метрономом; когда выбран, действия идут ровно через ActionDelay.
    /// Мгновенных серий быть не должно: игрок должен видеть каждый флаг.
    /// </summary>
    protected async Task DelayForAction(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        if (BotTurnTiming.IsTurnBased(_matchOptions.Type))
        {
            await Delay(ActionDelay, lifetime);
            return;
        }

        var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
        var remaining = Math.Max(0f, roundTime - elapsed);

        var paced = 0.5f + (float)Random.Shared.NextDouble() * 1.5f;
        paced = Math.Min(paced, remaining * 0.4f);

        await Delay(Math.Max(paced, ActionDelay), lifetime);
    }

    private float ActionDelay
    {
        get
        {
            var delay = ProfileConfig.ActionDelay;
            return delay <= 0f ? 0.3f : delay;
        }
    }

    protected Task Delay(float seconds, IReadOnlyLifetime lifetime)
    {
        return Task.Delay(TimeSpan.FromSeconds(seconds), lifetime.Token);
    }
}
