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

        if (isHeads)
        {
            var source = new DurationModifierSource(PlayerModifier.AdditionalMoves, config.WinMoves, "cointoss", 1);
            invoker.Modifiers.Add(snapshot, source);

            _roundActionService.Schedule(new ModifierRoundAction(invoker, source));
        }
        else
        {
            var newMoves = invoker.Moves.Left - config.LoseMoves;

            if (newMoves < 0)
                newMoves = 0;
            invoker.Moves.SetCurrent(snapshot, newMoves);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}