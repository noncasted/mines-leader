namespace Game.GamePlay;

// Whether the cheat commands and the oracle of the agent observation are allowed in this host: the game flag Cheats
// is on and the host runs in Development. The flag is read on every call (switched at runtime in the console), the
// environment is fixed at start.
public interface ICheatsAccess
{
    bool IsAllowed { get; }

    // Why the cheats are refused; meaningful when IsAllowed is false.
    string RefusalReason { get; }
}
