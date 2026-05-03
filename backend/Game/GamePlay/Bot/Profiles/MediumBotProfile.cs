using Cluster.Configs;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public class MediumBotProfile : BotProfileBase
{
    public MediumBotProfile(
        IBotConfig config,
        IBotContext botContext,
        IBotCellAction cellAction,
        IBotCardAction cardAction,
        IBotFlagAction flagAction,
        ISessionLogger sessionLogger,
        IGameRound round)
        : base(config, botContext, cellAction, cardAction, flagAction, sessionLogger, round)
    {
    }

    public override BotProfile Profile => BotProfile.Medium;
    public override int ConstraintDepth => 2;

    public override async Task ExecuteTurn(IReadOnlyLifetime lifetime)
    {
        var bot = _botContext.Bot;
        var profileConfig = _config.Value.CurrentProfileConfig;

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

            // Phase 1: Flags — level-1 + level-2 constraint-solving.
            await RunFlagPhase(profileConfig.FlagsPerRound, startTime, roundTime, lifetime);

            // Phase 2: Cards — optimal selection.
            await RunCardPhase(profileConfig.CardsUsePerRound, startTime, roundTime, lifetime);

            // Phase 3: Open cells — proven-safe via deeper constraint-solving.
            await RunCellPhase(profileConfig.CellsOpenPerRound, startTime, roundTime, lifetime);

            await WaitRemainingTime(startTime, roundTime, lifetime);
            EndTurn(startTime, roundTime);
        }
        catch (OperationCanceledException)
        {
            // Turn cancelled
        }
    }
}
