using Shared;

namespace Game.GamePlay;

public class Trebuchet : IBoardCard
{
    public Trebuchet(IPlayer owner, IBoard target)
    {
        _owner = owner;
        _target = target;
    }

    private readonly IPlayer _owner;
    private readonly IBoard _target;

    public void Use(Position position)
    {
                
    }
}