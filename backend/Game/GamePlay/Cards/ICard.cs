using Shared;

namespace Game.GamePlay;

public interface ICard<in TPayload> where TPayload : ICardUsePayload
{
    CardUseResult Use(IPlayer invoker, TPayload payload);
}

public class CardUseResult
{
    public required EmptyResponse Result { get; init; }
    public required ICardActionData? ActionData { get; init; }
}