namespace Shared
{
    // A session entity that outlives a change of the scene of the session (the local server and the game services
    // destroy only the other session entities then).
    public interface IPersistentSessionEntityPayload : ISessionEntityPayload
    {
    }
}
