using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Links the invoker and opponent so that mine damage dealt to the invoker is mirrored to the opponent for a set duration.
/// </summary>
public class SoulLink : ICard<CardUsePayload.SoulLink>
{
    public SoulLink(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(CardUseContext context, CardUsePayload.SoulLink payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.SoulLink_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        var source = new DurationModifierSource(PlayerModifier.SoulLink, 1, "soul_link", config.TurnsDuration);
        invoker.Modifiers.Add(snapshot, source);
        _roundActionService.Schedule(new ModifierRoundAction(invoker, source));

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.SoulLink()
        {
            TargetPlayer = opponent.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}
