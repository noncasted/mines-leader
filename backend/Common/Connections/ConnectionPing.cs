using Shared;

namespace Common;

public interface IConnectionPing
{
    Task<bool> Execute();
}

public class ConnectionPing : IConnectionPing
{
    public ConnectionPing(IConnectionWriter writer)
    {
        _writer = writer;
    }
    
    private readonly IConnectionWriter _writer;

    public async Task<bool> Execute()
    {
        var result = await _writer.WriteRequest<PingContext.Response>(PingContext.DefaultRequest);

        if (result == null)
            return false;

        return true;
    }
}