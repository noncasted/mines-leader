namespace Shared
{
    // Payload of a session entity: the entity belongs to the session, not to the user who created it. The backend keeps
    // it for the whole session (owner index 0 in CreatedOverview), the creator leaving doesn't destroy it. Its properties
    // are written by the current session owner by convention.
    public interface ISessionEntityPayload : IEntityPayload
    {
    }

    // A session entity that outlives a change of the scene of the session (the local server and the game services
    // destroy only the other session entities then).
    public interface IPersistentSessionEntityPayload : ISessionEntityPayload
    {
    }
}
