using Shared;

namespace Game.GamePlay;

/// <summary>
/// Flips a coin: heads doubles current mana, tails sets mana to zero.
/// </summary>
public class DoubleOrNothing : ICard<CardUsePayload.DoubleOrNothing>
{
    public DoubleOrNothing(IGameRandom gameRandom, IRoundActionService roundActionService)
    {
        _gameRandom = gameRandom;
        _roundActionService = roundActionService;
    }

    private readonly IGameRandom _gameRandom;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(CardUseContext context, CardUsePayload.DoubleOrNothing payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var isHeads = _gameRandom.FlipCoin(invoker);
        int resultMana;

        if (isHeads)
        {
            resultMana = invoker.Mana.Current * 2;
        }
        else
        {
            resultMana = 0;
        }

        snapshot.RecordCardUse(invoker.User.Id, context.CardId,
            new CardActionSnapshot.DoubleOrNothing()
            {
                TargetPlayer = invoker.User.Id,
                IsHeads = isHeads,
                ResultMana = resultMana
            });

        if (isHeads)
        {
            var bonus = invoker.Mana.Current;
            invoker.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMana, bonus);
            invoker.Mana.SetCurrent(snapshot, resultMana);

            _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, bonus), 1);
        }
        else
        {
            invoker.Mana.SetCurrent(snapshot, 0);
        }

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}