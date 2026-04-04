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
        IBotFlagAction flagAction)
    {
        _config = config;
        _round = round;
        _botContext = botContext;
        _context = context;
        _cellAction = cellAction;
        _cardAction = cardAction;
        _flagAction = flagAction;
    }

    private readonly IBotConfig _config;
    private readonly IGameRound _round;
    private readonly IGameContext _context;
    private readonly IBotContext _botContext;

    private readonly IBotCellAction _cellAction;
    private readonly IBotCardAction _cardAction;
    private readonly IBotFlagAction _flagAction;

    public async Task Run(IUser user)
    {
        _context.GameStarted.Advise(user.Lifetime, () =>
            {
                var bot = _context.Players.First(t => t.User.Id == user.Id);
                var opponent = _context.GetOpponent(bot);

                _botContext.Construct(bot, opponent);
            }
        );

        _round.CurrentPlayer.ViewNotNull(user.Lifetime, (roundLifetime, player) =>
            {
                if (player.User.Id != user.Id)
                    return;

                Task.Run(() => OnBotTurn(roundLifetime));
            }
        );
    }

    private async Task OnBotTurn(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var configValue = _config.Value;
        var delay = TimeSpan.FromSeconds(configValue.ActionDelay);

        try
        {
            // Пауза перед ходом - бот "думает"
            await Task.Delay(delay, lifetime.Token);

            if (_botContext.Bot.Board.Cells.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2f), lifetime.Token);
                _cellAction.TryExecute();
                await Task.Delay(TimeSpan.FromSeconds(1f), lifetime.Token);
            }

            for (var i = 0; i < configValue.FlagsPerRound; i++)
            {
                if (_flagAction.TryExecute() == false)
                    break;

                await Task.Delay(delay, lifetime.Token);
            }

            await Task.Delay(delay, lifetime.Token);

            for (var i = 0; i < configValue.CellsOpenPerRound; i++)
            {
                if (bot.Moves.Left <= 0)
                    break;

                if (_cellAction.TryExecute() == false)
                    break;

                await Task.Delay(delay, lifetime.Token);
            }

            await Task.Delay(delay, lifetime.Token);

            for (var i = 0; i < configValue.CardsUsePerRound; i++)
            {
                if (bot.Moves.Left <= 0)
                    break;

                var usedCard = _cardAction.TryExecute(lifetime);

                if (usedCard == false)
                    continue;

                await Task.Delay(delay, lifetime.Token);
            }

            await Task.Delay(delay, lifetime.Token);

            _round.SkipTurn();
        }
        catch (OperationCanceledException)
        {
            // Ход отменен (пользователь отключился)
        }
        catch (Exception e)
        {
            throw;
        }
    }
}