using Cluster.Configs;
using Game.GamePlay.Snapshots;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public interface IBotCellAction
{
    bool TryExecute();
}

public class BotCellAction : IBotCellAction
{
    public BotCellAction(
        IBotConfig config,
        IBotCommandUtils commandUtils,
        IBotContext context,
        ISessionLogger sessionLogger,
        MatchCreateOptions matchOptions)
    {
        _config = config;
        _commandUtils = commandUtils;
        _context = context;
        _sessionLogger = sessionLogger;
        _matchOptions = matchOptions;
    }

    private readonly IBotConfig _config;
    private readonly IBotCommandUtils _commandUtils;
    private readonly IBotContext _context;
    private readonly ISessionLogger _sessionLogger;
    private readonly MatchCreateOptions _matchOptions;

    public bool TryExecute()
    {
        if (_context.Bot.Moves.Left <= 0)
        {
            _sessionLogger.LogBotAction("Cell", "No moves left");
            return false;
        }

        var board = _context.Bot.Board;
        var depth = GetConstraintDepth();

        if (board.Cells.Count == 0)
        {
            var randomPosition = board.RandomPosition();

            _commandUtils.WithSnapshot(snapshot => {
                _context.Bot.Moves.OnUsed(snapshot);

                board.EnsureGenerated(snapshot, randomPosition);
                snapshot.RecordReveal(board, randomPosition);

                _context.Bot.Actions.OnCellOpened();
            });

            _sessionLogger.LogBotAction("Cell", $"First cell opened at {randomPosition}");
            return true;
        }

        // A chord and a single open cost one move, so we choose by the number of cells
        // that will actually open together with the cascade.
        var chord = BotOpenPlans.FindBestChord(board);
        var single = BotOpenPlans.FindBestSafeCell(board, depth);

        var plan = chord?.Gain >= (single?.Gain ?? 0) ? chord : single;

        if (plan != null)
            return Open(plan.Value);

        if (MatchBotProfile.Resolve(_matchOptions, _config) == BotProfile.Hard &&
            BotMineGuess.TryFind(board, out var target, out var reason))
        {
            return Open(new BotOpenPlan([target], 1, reason));
        }

        _sessionLogger.LogBotAction("Cell", "No safe cell found | No constraint-proven safe cells");
        return false;
    }

    private bool Open(BotOpenPlan plan)
    {
        var board = _context.Bot.Board;

        _commandUtils.WithSnapshot(snapshot => {
            _context.Bot.Moves.OnUsed(snapshot);

            foreach (var position in plan.Seeds)
            {
                var cell = board.Cells[position];

                if (cell.Status != CellStatus.Taken)
                    continue;

                var taken = cell.AsTaken();

                if (taken.HasMine == false)
                    continue;

                _context.Bot.Health.TakeDamage(snapshot, 1);
                snapshot.RecordExplosion(board, position);
                taken.Explode();
                taken.ToFree();
                board.RegisterDetonatedMine();
                snapshot.RecordCellFree(board, position);
            }

            snapshot.RecordReveal(board, plan.Seeds);

            _context.Bot.Actions.OnCellOpened();
        });

        _sessionLogger.LogBotAction("Cell",
            $"Opened {plan.Seeds.Count} seed(s) at {plan.Seeds[0]} | {plan.Reason} | MovesLeft={_context.Bot.Moves.Left}");

        return true;
    }

    private int GetConstraintDepth()
    {
        return MatchBotProfile.Resolve(_matchOptions, _config) == BotProfile.Easy ? 1 : 2;
    }
}
