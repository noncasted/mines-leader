using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

/// <summary>
/// Swaps a diamond area between the owner's and opponent's fields, transferring all cell states.
/// </summary>
public class DimensionRift : ICard<CardUsePayload.DimensionRift>
{
    public DimensionRift(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.DimensionRift payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        var opponentBoard = opponent.Board;
        var ownerBoard = invoker.Board;
        opponentBoard.EnsureGenerated(payload.Position);
        ownerBoard.EnsureGenerated(payload.Position);

        // TODO: Full implementation requires complete cell state swap logic.
        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.DimensionRift()
            {
                TargetPlayer = opponentBoard.OwnerId
            }
        };
    }
}