using Shared;

namespace Backend.Matches;

public interface IMatchmaking
{
    Task Search(Guid userId, SessionType type);
    Task CancelSearch(Guid userId);
    Task Create(Guid userId);
}