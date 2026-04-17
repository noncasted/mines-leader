using Cluster.Configs;
using Shared;

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

    public CardUseResult Use(CardUseContext context, CardUsePayload.Siphon payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var config = _configs.Value.Siphon_Normal;
        var opponent = _gameContext.GetOpponent(invoker);

        opponent.Mana.SetMax(snapshot, opponent.Mana.Max - config.DrainAmount);
        invoker.Mana.SetMax(snapshot, invoker.Mana.Max + config.DrainAmount);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.Siphon()
        {
            TargetPlayer = opponent.User.Id
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}