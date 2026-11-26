namespace Shared
{
    // Payload of a session entity: the entity belongs to the session, not to the user who created it. The backend keeps
    // it for the whole session (owner index 0 in CreatedOverview), the creator leaving doesn't destroy it. Its properties
    // are written by the current session owner by convention.
    public interface ISessionEntityPayload : IEntityPayload
    {
    }
}
