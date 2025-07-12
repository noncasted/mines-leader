using Shared;

namespace Game;

public interface ISessionSearch
{
    Guid GetOrCreate(SessionSearchParameters parameters);
}

public class SessionSearchParameters
{
    public required SessionType Type { get; init; }
}

public class SessionSearch : ISessionSearch
{
    public SessionSearch(ISessionFactory factory, ISessionsCollection collection)
    {
        _factory = factory;
        _collection = collection;
    }

    private readonly ISessionFactory _factory;
    private readonly ISessionsCollection _collection;

    public Guid GetOrCreate(SessionSearchParameters parameters)
    {
        foreach (var (id, session) in _collection.Entries)
        {
            if (session.CreateOptions.Type != parameters.Type || session.Lifetime.IsTerminated == true)
                continue;

            return id;
        }

        return _factory.Create(new SessionCreateOptions()
        {
            ExpectedUsers = 0,
            Type = parameters.Type
        });
    }
}