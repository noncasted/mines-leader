using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Increases all of the opponent's card costs for their next turn via the ManaCostPenalty modifier.
/// </summary>
public class Embargo : ICard<CardUsePayload.Embargo>
{
    public Embargo(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext)
    {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Embargo payload)
    {
        var config = _configs.Value.Embargo_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        opponent.Modifiers.Inc(PlayerModifier.ManaCostPenalty, config.CostIncrease);

        _roundActionService.Schedule(
            new ModifierDisposeAction(opponent, PlayerModifier.ManaCostPenalty, config.CostIncrease), 1);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Embargo()
            {
                TargetPlayer = opponent.User.Id
            }
        };
    }
}