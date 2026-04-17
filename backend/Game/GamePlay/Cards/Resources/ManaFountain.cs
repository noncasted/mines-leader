using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Rolls a random amount within a configured range and grants that much temporary mana this turn.
/// </summary>
public class ManaFountain : ICard<CardUsePayload.ManaFountain>
{
    public ManaFountain(ICardConfigs configs, IRoundActionService roundActionService, IGameRandom gameRandom)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameRandom = gameRandom;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameRandom _gameRandom;

    public CardUseResult Use(CardUseContext context, CardUsePayload.ManaFountain payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.ManaFountain_Normal;
        var rolled = _gameRandom.Range(invoker, config.MinMana, config.MaxMana);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId,
            new CardActionSnapshot.ManaFountain()
            {
                TargetPlayer = invoker.User.Id,
                RolledAmount = rolled
            });

        invoker.Modifiers.Inc(snapshot, PlayerModifier.AdditionalMana, rolled);
        invoker.Mana.SetCurrent(snapshot, invoker.Mana.Current + rolled);

        _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, rolled), 1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}