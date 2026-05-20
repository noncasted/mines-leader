using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Reduces opponent's available moves for a number of rounds via the AdditionalMoves modifier.
/// </summary>
public class Lockdown : ICard<CardUsePayload.Lockdown>
{
    public Lockdown(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.Lockdown payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.Lockdown_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        var source = new DurationModifierSource(PlayerModifier.AdditionalMoves, -config.MovesReduction, "lockdown", config.TurnsDuration);
        opponent.Modifiers.Add(snapshot, source);

        _roundActionService.Schedule(new ModifierRoundAction(opponent, source));

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Lockdown
        {
            TargetPlayer = opponent.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}