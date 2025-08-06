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
    public Player(IEntity entity, Health health, Mana mana, Modifiers modifiers)
    {
        _entity = entity;
        _health = health;
        _mana = mana;
        _modifiers = modifiers;
    }

    private readonly IEntity _entity;
    private readonly Health _health;
    private readonly Mana _mana;
    private readonly Modifiers _modifiers;
    
    public IBoard Board { get; }
    public IHealth Health => _health;
    public IMana Mana => _mana;
    public IModifiers Modifiers => _modifiers;
}