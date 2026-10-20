namespace Game.Session;

// Entity ids of a session: every user owns the range userIndex * Stride + counter, counter 1..Stride-1. The backend
// builds the entities of a user in its range (EntityBuilder), and a client may create only in its own range: an id
// of another range would be taken by that user's entity later (its player, built by the session).
public static class SessionEntityIds
{
    public const int Stride = 100_000;

    public static bool IsInRangeOf(int entityId, int userIndex)
    {
        return entityId > 0 && entityId % Stride != 0 && entityId / Stride == userIndex;
    }
}
