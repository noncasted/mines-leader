using Shared;

namespace Game.GamePlay;

public interface ICard<in TPayload> where TPayload : ICardUsePayload
{
    CardUseResult Use(CardUseContext context, TPayload payload);
}

public class CardUseContext
{
    public required IPlayer Invoker { get; init; }
    public required MoveSnapshot Snapshot { get; init; }
    public required Guid CardId { get; init; }
}

public class CardUseResult
{
    public required EmptyResponse Result { get; init; }
}