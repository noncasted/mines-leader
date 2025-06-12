namespace Backend.Matches;

public interface ILobbyFactory
{
    Task GetOrCreate(Guid userId);
}