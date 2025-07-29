namespace Game.GamePlay;

public interface IPlayer
{
    IBoard Board { get; }
    IHealth Health { get; }
    IMana Mana { get; }
    IModifiers Modifiers { get; }
}

public class Player : IPlayer
{
    public IBoard Board { get; }
    public IHealth Health { get; }
    public IMana Mana { get; }
    public IModifiers Modifiers { get; }
}