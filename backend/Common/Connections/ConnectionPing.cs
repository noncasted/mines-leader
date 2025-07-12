namespace Common;

public interface IConnectionPing
{
    Task<bool> Execute(IReadOnlyLifetime lifetime);
}

public class ConnectionPing : IConnectionPing
{
    public ConnectionPing(IConnection connection)
    {
        _connection = connection;
    }
    
    private readonly IConnection _connection;

    public Task<bool> Execute(IReadOnlyLifetime lifetime)
    {
           
    }
}