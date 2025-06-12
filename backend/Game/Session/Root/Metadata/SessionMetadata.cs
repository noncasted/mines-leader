namespace Game;

public class SessionMetadata : ISessionMetadata
{
    public SessionMetadata(int expectedUsers, string type)
    {
        ExpectedUsers = expectedUsers;
        Type = type;
    }

    public int ExpectedUsers { get; }
    public string Type { get; }
}