using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Reduces opponent's available moves for a number of rounds via the AdditionalMoves modifier.
/// </summary>
public class Lockdown : ICard<CardUsePayload.Lockdown> {
    public Lockdown(ICardConfigs configs, IRoundActionService roundActionService, IGameContext gameContext) {
        _configs = configs;
        _roundActionService = roundActionService;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Lockdown payload) {
        var config = _configs.Value.Lockdown_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        opponent.Modifiers.Dec(PlayerModifier.AdditionalMoves, config.MovesReduction);
        opponent.Moves.Refresh();

        _roundActionService.Schedule(
            new ModifierDisposeAction(opponent, PlayerModifier.AdditionalMoves, -config.MovesReduction), config.Duration);

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Lockdown {
                TargetPlayer = opponent.User.Id
            }
        };
    }
}
