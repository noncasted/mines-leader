namespace Game.GamePlay;

public interface IMana
{
    int Current { get; }
    int Max { get; }

    void Use(int amount);
    void Restore(int amount);
}

public class Mana : IMana
{
    public int Current { get; }
    public int Max { get; }

    public void Use(int amount)
    {
        
    }

    public void Restore(int amount)
    {
    }
}