using Cluster.Configs;
using Common.Reactive;
using Game.Session;

namespace Game.GamePlay;

public interface IBotRunner
{
    Task Run(IUser user);
}

public class BotRunner : IBotRunner
{
    public BotRunner(
        IBotConfig config,
        IGameRound round,
        IBotContext botContext,
        IGameContext context,
        IBotCellAction cellAction,
        IBotCardAction cardAction,
        IBotFlagAction flagAction,
        ISessionLogger sessionLogger)
    {
        _config = config;
        _round = round;
        _botContext = botContext;
        _context = context;
        _cellAction = cellAction;
        _cardAction = cardAction;
        _flagAction = flagAction;
        _sessionLogger = sessionLogger;
    }

    private readonly IBotConfig _config;
    private readonly IGameRound _round;
    private readonly IGameContext _context;
    private readonly IBotContext _botContext;
    private readonly ISessionLogger _sessionLogger;

    private readonly IBotCellAction _cellAction;
    private readonly IBotCardAction _cardAction;
    private readonly IBotFlagAction _flagAction;

    public async Task Run(IUser user)
    {
        _context.GameStarted.Advise(user.Lifetime, () => {
            var bot = _context.Players.First(t => t.User.Id == user.Id);
            var opponent = _context.GetOpponent(bot);

            _botContext.Construct(bot, opponent);
        });

        _round.CurrentPlayer.ViewNotNull(user.Lifetime, (roundLifetime, player) => {
            if (player.User.Id != user.Id)
                return;

            // Skip false trigger during initialization (moves not yet restored)
            if (player.Moves.IsAvailable == false)
                return;

            Task.Run(() => OnBotTurn(roundLifetime));
        });
    }

    private async Task OnBotTurn(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var configValue = _config.Value;

        // Random round duration for human-like pacing
        var roundTime = configValue.MinRoundTime +
                        (float)Random.Shared.NextDouble() * (configValue.MaxRoundTime - configValue.MinRoundTime);

        try
        {
            _sessionLogger.LogBotTurnStart(bot.User.Id);

            var handCards = string.Join(", ", bot.Hand.Entries.Select(c => c.Type));

            _sessionLogger.LogBotAction("State",
                $"Mana={bot.Mana.Current}/{bot.Mana.Max} Moves={bot.Moves.Left}/{bot.Moves.Max} Hand=[{handCards}] Health={bot.Health.Current.Value}");

            var startTime = DateTime.UtcNow;

            // Initial "thinking" pause
            await Delay(1.5f, lifetime);

            if (_botContext.Bot.Board.Cells.Count == 0)
            {
                _sessionLogger.LogBotAction("Init", "Board empty, opening first cell");
                await Delay(2f, lifetime);
                _cellAction.TryExecute();
                await Delay(1f, lifetime);
            }

            // Collect actions — interleave card, flag, cell to look more human
            var cardsUsed = 0;
            var flagsPlaced = 0;
            var cellsOpened = 0;
            var cardsExhausted = false;
            var flagsExhausted = false;
            var cellsExhausted = false;

            while (cardsExhausted == false || flagsExhausted == false || cellsExhausted == false)
            {
                var didSomething = false;

                // One card
                if (cardsExhausted == false)
                {
                    if (cardsUsed >= configValue.CardsUsePerRound || bot.Moves.Left <= 0)
                    {
                        cardsExhausted = true;
                    }
                    else
                    {
                        var usedCard = _cardAction.TryExecute(lifetime);

                        if (usedCard)
                        {
                            cardsUsed++;
                            didSomething = true;
                            await DelayForAction(startTime, roundTime, lifetime);
                        }
                        else
                        {
                            cardsExhausted = true;
                        }
                    }
                }

                // One flag
                if (flagsExhausted == false)
                {
                    if (flagsPlaced >= configValue.FlagsPerRound)
                    {
                        flagsExhausted = true;
                    }
                    else
                    {
                        var placed = _flagAction.TryExecute();

                        if (placed)
                        {
                            flagsPlaced++;
                            didSomething = true;
                            await DelayForAction(startTime, roundTime, lifetime);
                        }
                        else
                        {
                            flagsExhausted = true;
                        }
                    }
                }

                // One cell
                if (cellsExhausted == false)
                {
                    if (bot.Moves.Left <= 0)
                    {
                        cellsExhausted = true;
                    }
                    else
                    {
                        var opened = _cellAction.TryExecute();

                        if (opened)
                        {
                            cellsOpened++;
                            didSomething = true;
                            await DelayForAction(startTime, roundTime, lifetime);
                        }
                        else
                        {
                            cellsExhausted = true;
                        }
                    }
                }

                if (didSomething == false)
                    break;
            }

            if (cardsUsed > 0)
                _sessionLogger.LogBotAction("Cards", $"Used {cardsUsed} cards");

            if (flagsPlaced > 0)
                _sessionLogger.LogBotAction("Flags", $"Placed {flagsPlaced} flags");

            if (cellsOpened > 0)
                _sessionLogger.LogBotAction("Cells", $"Opened {cellsOpened} cells");

            // Wait remaining time budget before ending turn
            var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var remaining = roundTime - elapsed;

            if (remaining > 0.5f)
                await Delay(remaining, lifetime);

            _sessionLogger.LogBotAction("EndTurn",
                $"Flags={flagsPlaced} Cells={cellsOpened} Cards={cardsUsed} MovesLeft={bot.Moves.Left} Time={elapsed:F1}s/{roundTime:F1}s");
            _round.SkipTurn();
        }
        catch (OperationCanceledException)
        {
            // Ход отменен (пользователь отключился)
        }
    }

    /// <summary>
    /// Delay proportional to remaining time budget, with some randomness.
    /// </summary>
    private async Task DelayForAction(DateTime startTime, float roundTime, IReadOnlyLifetime lifetime)
    {
        var elapsed = (float)(DateTime.UtcNow - startTime).TotalSeconds;
        var remaining = roundTime - elapsed;

        if (remaining <= 0.5f)
            return;

        // Random delay: 0.5-2s, but don't exceed remaining budget
        var delay = 0.5f + (float)Random.Shared.NextDouble() * 1.5f;
        delay = Math.Min(delay, remaining * 0.4f);
        delay = Math.Max(delay, 0.3f);

        await Delay(delay, lifetime);
    }

    private static Task Delay(float seconds, IReadOnlyLifetime lifetime)
    {
        return Task.Delay(TimeSpan.FromSeconds(seconds), lifetime.Token);
    }
}