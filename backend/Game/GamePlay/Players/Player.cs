namespace Game.GamePlay;

public interface IPlayer
{
    IUser User { get; }
    IBoard Board { get; }
    IStash Stash { get; }
    IDeck Deck { get; }
    IHealth Health { get; }
    IHand Hand { get; }
    IMana Mana { get; }
    IMoves Moves { get; }
    IModifiers Modifiers { get; }
}

public class Player : IPlayer
{
    public Player(
        IEntity entity,
        Board board,
        Health health,
        Mana mana, 
        Modifiers modifiers,
        Deck deck,
        Moves moves,
        Hand hand, Stash stash)
    {
        _entity = entity;
        _board = board;
        _health = health;
        _mana = mana;
        _modifiers = modifiers;
        _deck = deck;
        _moves = moves;
        _hand = hand;
        _stash = stash;
    }

    private readonly IEntity _entity;
    private readonly Board _board;
    private readonly Health _health;
    private readonly Mana _mana;
    private readonly Modifiers _modifiers;
    private readonly Stash _stash;
    private readonly Deck _deck;
    private readonly Moves _moves;
    private readonly Hand _hand;

    public IUser User => _entity.Owner;
    public IBoard Board => _board;
    public IStash Stash => _stash;
    public IDeck Deck => _deck;
    public IHealth Health => _health;
    public IHand Hand => _hand;
    public IMana Mana => _mana;
    public IMoves Moves => _moves;
    public IModifiers Modifiers => _modifiers;
}