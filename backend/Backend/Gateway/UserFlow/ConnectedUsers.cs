using Common;

namespace Backend.Gateway;

public class ConnectedUsers : IConnectedUsers
{
    private readonly ViewableDelegate<IUserConnection> _connected = new();
    private readonly Dictionary<Guid, UserConnection> _entriesInternal = new();
    private readonly Dictionary<Guid, IUserConnection> _entries = new();

    public IViewableDelegate<IUserConnection> Connected => _connected;
    public IReadOnlyDictionary<Guid, IUserConnection> Entries => _entries;

    public void OnConnected(Guid userId, string connectionId)
    {
        if (_entriesInternal.TryGetValue(userId, out var existing))
            existing.InternalLifetime.Terminate();

        var lifetime = new Lifetime();
        var connection = new UserConnection
        {
            UserId = userId,
            ConnectionId = connectionId,
            InternalLifetime = lifetime
        };

        _entriesInternal[userId] = connection;
        _entries[userId] = connection;

        _connected.Invoke(connection);
    }

    public void OnDisconnected(Guid userId)
    {
        if (_entriesInternal.TryGetValue(userId, out var existing) == false)
            return;

        existing.InternalLifetime.Terminate();
        _entriesInternal.Remove(userId);
        _entries.Remove(userId);
    }

    public bool IsConnected(Guid userId)
    {
        return _entriesInternal.ContainsKey(userId);
    }
}