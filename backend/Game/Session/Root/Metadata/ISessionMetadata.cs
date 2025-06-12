namespace Game;

public interface ISessionMetadata
{
    int ExpectedUsers { get; }
    string Type { get; }
}