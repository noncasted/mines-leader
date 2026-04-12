using Shared;

namespace Game.GamePlay;

/// <summary>
/// Copies the last card played by the opponent and applies its effect for the owner.
/// </summary>
public class MirrorMatch : ICard<CardUsePayload.MirrorMatch>
{
    public MirrorMatch(IGameContext gameContext, IMoveSnapshotAccessor snapshotAccessor)
    {
        _gameContext = gameContext;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly IGameContext _gameContext;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.MirrorMatch payload)
    {
        // TODO: Full implementation requires LastUsedCard tracking on IPlayer.
        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.MirrorMatch()
            {
                TargetPlayer = invoker.User.Id,
                CopiedCard = CardType.MirrorMatch
            }
        };
    }
}