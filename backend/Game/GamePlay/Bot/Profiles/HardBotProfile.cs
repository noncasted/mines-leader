using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay.Profiles;

public class HardBotProfile : BotProfileBase
{
    public HardBotProfile(
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

    public override BotProfile Profile => BotProfile.Hard;
    public override int ConstraintDepth => 2;

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

            // Phase 1: Flags — deep constraint-solving, maximum flags per round.
            await RunFlagPhase(profileConfig.FlagsPerRound, startTime, roundTime, lifetime);

            // Phase 2: Cards — full pool, optimal selection.
            await RunCardPhase(profileConfig.CardsUsePerRound, startTime, roundTime, lifetime);

            // Phase 3: Open cells — only if moves remain and no unflagged mines.
            await RunCellPhase(profileConfig.CellsOpenPerRound, startTime, roundTime, lifetime);

            await WaitBeforeEndTurn(startTime, roundTime, lifetime);
            EndTurn(startTime, roundTime);
        }
        catch (OperationCanceledException)
        {
            // Turn cancelled
        }
    }
}
