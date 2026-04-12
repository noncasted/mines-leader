using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Permanently drains max mana from the opponent and transfers it to the invoker.
/// </summary>
public class Siphon : ICard<CardUsePayload.Siphon>
{
    public Siphon(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Siphon payload)
    {
        var config = _configs.Value.Siphon_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        opponent.Mana.SetMax(opponent.Mana.Max - config.DrainAmount);
        invoker.Mana.SetMax(invoker.Mana.Max + config.DrainAmount);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Siphon()
            {
                TargetPlayer = opponent.User.Id
            }
        };
    }
}