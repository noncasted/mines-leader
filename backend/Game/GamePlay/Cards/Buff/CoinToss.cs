using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads grants extra moves, tails costs moves.
/// </summary>
public class CoinToss : ICard<CardUsePayload.CoinToss>
{
    public CoinToss(ICardConfigs configs, IGameRandom gameRandom, IRoundActionService roundActionService)
    {
        _configs = configs;
        _gameRandom = gameRandom;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;
    private readonly IRoundActionService _roundActionService;

    public const string HeadsKey = "cointoss";
    public const string TailsKey = "cointoss_tails";

    public CardUseResult Use(CardUseContext context, CardUsePayload.CoinToss payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.CoinToss_Normal;
        var isHeads = _gameRandom.FlipCoin(invoker);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId,
            new CardActionSnapshot.CoinToss()
            {
                TargetPlayer = invoker.User.Id,
                IsHeads = isHeads
            });

        // Both sides of the coin are a max-moves modifier: tails is a debuff,
        // not a one-off deduction of current moves. The source keys differ so that
        // heads and tails show up as separate entries in the buffs.
        var source = isHeads
            ? new DurationModifierSource(PlayerModifier.AdditionalMoves, config.WinMoves, HeadsKey, 1)
            : new DurationModifierSource(PlayerModifier.AdditionalMoves, -config.LoseMoves, TailsKey, 1);

        invoker.Modifiers.Add(snapshot, source);
        _roundActionService.Schedule(new ModifierRoundAction(invoker, source));

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}