using Shared;

namespace Game.GamePlay;

public interface ICard
{
    EmptyResponse Use(ICardUsePayload payload);
}