using Shared;

namespace Game.GamePlay;

/// <summary>
/// Swaps a diamond area between the owner's and opponent's fields, transferring all cell states.
/// </summary>
public class DimensionRift : ICard
{
    public DimensionRift(
        IPlayer owner,
        IBoard ownerBoard,
        IBoard opponentBoard,
        CardConfigOptions.DimensionRift config,
        CardUsePayload.DimensionRift payload)
    {
        _owner = owner;
        _ownerBoard = ownerBoard;
        _opponentBoard = opponentBoard;
        _config = config;
        _payload = payload;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _ownerBoard;
    private readonly IBoard _opponentBoard;
    private readonly CardConfigOptions.DimensionRift _config;
    private readonly CardUsePayload.DimensionRift _payload;

    public CardUseResult Use()
    {
        // TODO: Full implementation requires complete cell state swap logic.
        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.DimensionRift()
            {
                TargetPlayer = _opponentBoard.OwnerId
            }
        };
    }
}
