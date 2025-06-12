namespace Game;

public interface ISessionMetadata
{
    int ExpectedUsers { get; }
    string Type { get; }
}

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