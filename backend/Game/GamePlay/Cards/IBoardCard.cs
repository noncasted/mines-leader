using Shared;

namespace Game.GamePlay;

public interface IBoardCard
{
    EmptyResponse Use(Position position);
}