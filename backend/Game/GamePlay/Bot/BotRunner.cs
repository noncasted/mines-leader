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

            Task.Run(() => OnBotTurn(roundLifetime));
        });
    }

    private async Task OnBotTurn(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var configValue = _config.Value;
        var delay = TimeSpan.FromSeconds(configValue.ActionDelay);

        try
        {
            _sessionLogger.LogBotTurnStart(bot.User.Id);

            // Пауза перед ходом - бот "думает"
            await Task.Delay(delay, lifetime.Token);

            if (_botContext.Bot.Board.Cells.Count == 0)
            {
                _sessionLogger.LogBotAction("Init", "Board empty, opening first cell");
                await Task.Delay(TimeSpan.FromSeconds(2f), lifetime.Token);
                _cellAction.TryExecute();
                await Task.Delay(TimeSpan.FromSeconds(1f), lifetime.Token);
            }

            var flagsPlaced = 0;

            for (var i = 0; i < configValue.FlagsPerRound; i++)
            {
                if (_flagAction.TryExecute() == false)
                    break;

                flagsPlaced++;
                await Task.Delay(delay, lifetime.Token);
            }

            if (flagsPlaced > 0)
                _sessionLogger.LogBotAction("Flags", $"Placed {flagsPlaced} flags");

            await Task.Delay(delay, lifetime.Token);

            var cellsOpened = 0;

            for (var i = 0; i < configValue.CellsOpenPerRound; i++)
            {
                if (bot.Moves.Left <= 0)
                    break;

                if (_cellAction.TryExecute() == false)
                    break;

                cellsOpened++;
                await Task.Delay(delay, lifetime.Token);
            }

            if (cellsOpened > 0)
                _sessionLogger.LogBotAction("Cells", $"Opened {cellsOpened} cells");

            await Task.Delay(delay, lifetime.Token);

            var cardsUsed = 0;

            for (var i = 0; i < configValue.CardsUsePerRound; i++)
            {
                if (bot.Moves.Left <= 0)
                    break;

                var usedCard = _cardAction.TryExecute(lifetime);

                if (usedCard == false)
                    continue;

                cardsUsed++;
                await Task.Delay(delay, lifetime.Token);
            }

            if (cardsUsed > 0)
                _sessionLogger.LogBotAction("Cards", $"Used {cardsUsed} cards");

            await Task.Delay(delay, lifetime.Token);

            _sessionLogger.LogBotAction("EndTurn", $"Flags={flagsPlaced} Cells={cellsOpened} Cards={cardsUsed}");
            _round.SkipTurn();
        }
        catch (OperationCanceledException)
        {
            // Ход отменен (пользователь отключился)
        }
    }
}