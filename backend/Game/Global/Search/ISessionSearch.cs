namespace Game;

public interface ISessionSearch
{
    Task<Guid> GetOrCreate(SessionSearchParameters parameters);
}

public class SessionSearchParameters
{
    public required string Type { get; init; }
}