namespace Game.GamePlay;

public interface IHealth
{
    int Current { get; }
    int Max { get; }

    void TakeDamage(int damage);
    void Heal(int amount);
}

public class Health
{
    
}