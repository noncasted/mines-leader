using Shared;

namespace Game.GamePlay;

public interface ICard
{
    CardUseResult Use();
}

public class CardUseResult
{
    public required EmptyResponse Result { get; init; }
    public required ICardActionData? ActionData { get; init; }
}