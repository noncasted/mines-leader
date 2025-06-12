namespace Game;

public class SessionSearch : ISessionSearch
{
    public SessionSearch(ISessionFactory factory, ISessionsCollection collection)
    {
        _factory = factory;
        _collection = collection;
    }

    private readonly ISessionFactory _factory;
    private readonly ISessionsCollection _collection;

    public async Task<Guid> GetOrCreate(SessionSearchParameters parameters)
    {
        foreach (var (id, session) in _collection.Entries)
        {
            if (session.Metadata.Type != parameters.Type)
                continue;

            return id;
        }

        return await _factory.Create(new SessionCreateOptions()
        {
            ExpectedUsers = 0,
            Type = parameters.Type
        });
    }
}