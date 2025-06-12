namespace Backend.Matches;

public interface IMatchmaking
{
    Task Search(Guid userId, string type);
    Task CancelSearch(Guid userId);
    Task Create(Guid userId);
}