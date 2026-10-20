namespace Game.Session;

// A request the session refuses by the rules (an entity id out of the range of its creator, a taken id): the client
// gets ErrorResponse with this code, the session logs a warning - a refusal, not a failure of the backend.
public class SessionRequestException : Exception
{
    public SessionRequestException(Shared.ErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    public Shared.ErrorCode Code { get; }
}
