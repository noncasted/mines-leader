using Cluster.Configs;
using Internal;
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
    /// Flag limit per turn. One budget lives for the whole turn so that passes between cell
    /// openings do not together exceed the profile's FlagsPerRound.
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

        // No limit on the number of cards: the bot plays while there are suitable cards, mana and moves.
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
    /// The loop "flags, open, flags": each opened cell gives new numbers, flags are placed on them
    /// at once, and flags open chords. The first flag pass picks up cells
    /// opened by cards. The loop runs while there are moves and logically deducible cells.
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
    /// A pause before ending the turn so the player can see the bot's last actions.
    /// The round may end earlier, then the wait is cut off together with the lifetime.
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
    /// A pause between cards: a played card should hang on the table,
    /// not fly to the stash together with the next one. When the round budget is spent,
    /// the pause shrinks to ActionDelay, but not to zero.
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
    /// A pause between flags and openings. While the round budget is not spent, the pace floats
    /// so the bot does not look like a metronome; once it is spent, actions go exactly ActionDelay apart.
    /// There must be no instant series: the player must see every flag.
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
