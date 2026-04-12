using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Rolls a random amount within a configured range and grants that much temporary mana this turn.
/// </summary>
public class ManaFountain : ICard<CardUsePayload.ManaFountain>
{
    public ManaFountain(
        ICardConfigs configs,
        IRoundActionService roundActionService,
        IGameRandom gameRandom,
        IMoveSnapshotAccessor snapshotAccessor)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameRandom = gameRandom;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameRandom _gameRandom;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ManaFountain payload)
    {
        var config = _configs.Value.ManaFountain_Normal;
        var rolled = _gameRandom.Range(invoker, config.MinMana, config.MaxMana);

        _snapshotAccessor.Snapshot.RecordCardUse(invoker.User.Id, _snapshotAccessor.CardId,
            new CardActionSnapshot.ManaFountain()
            {
                TargetPlayer = invoker.User.Id,
                RolledAmount = rolled
            });

        invoker.Modifiers.Inc(PlayerModifier.AdditionalMana, rolled);
        invoker.Mana.SetCurrent(invoker.Mana.Current + rolled);

        _roundActionService.Schedule(new ModifierDisposeAction(invoker, PlayerModifier.AdditionalMana, rolled), 1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = null
        };
    }
}