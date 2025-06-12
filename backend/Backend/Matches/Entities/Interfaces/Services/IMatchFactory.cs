namespace Backend.Matches;

public interface IMatchFactory
{
    Task Create(IReadOnlyList<Guid> participants);
}