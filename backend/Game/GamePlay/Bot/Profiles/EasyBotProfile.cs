using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay.Profiles;

public class EasyBotProfile : BotProfileBase
{
    public EasyBotProfile(
        IBotConfig config,
        IBotContext botContext,
        IBotCellAction cellAction,
        IBotCardAction cardAction,
        IBotFlagAction flagAction,
        ISessionLogger sessionLogger,
        IGameRound round,
        MatchCreateOptions matchOptions)
        : base(config, botContext, cellAction, cardAction, flagAction, sessionLogger, round, matchOptions)
    {
    }

    public override BotProfile Profile => BotProfile.Easy;
    public override int ConstraintDepth => 1;

    public override async Task ExecuteTurn(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var profileConfig = MatchBotProfile.ResolveConfig(_matchOptions, _config);

        var roundTime = profileConfig.MinRoundTime +
                        (float)Random.Shared.NextDouble() * (profileConfig.MaxRoundTime - profileConfig.MinRoundTime);

        try
        {
            _sessionLogger.LogBotTurnStart(bot.User.Id);
            LogTurnStart();

            var startTime = DateTime.UtcNow;
            await Delay(1.5f, lifetime);

            if (bot.Board.Cells.Count == 0)
                await OpenFirstCell(lifetime, 2f, 1f);

            var flagBudget = new FlagBudget(profileConfig.FlagsPerRound);

            // Phase 1: Flags — logical constraint-solving.
            await RunFlagPhase(flagBudget, startTime, roundTime, lifetime);

            // Phase 2: Cards — optimal selection.
            await RunCardPhase(startTime, roundTime, lifetime);

            // Phase 3: Flags after cards, then open cells and re-flag after each open.
            await RunSolveLoop(flagBudget, startTime, roundTime, lifetime);

            await WaitBeforeEndTurn(startTime, roundTime, lifetime);
            EndTurn(startTime, roundTime);
        }
        catch (OperationCanceledException)
        {
            // Turn cancelled
        }
    }
}
