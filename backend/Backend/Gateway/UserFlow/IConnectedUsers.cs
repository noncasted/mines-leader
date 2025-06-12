using Common;

namespace Backend.Gateway;

public interface IConnectedUsers
{
    IViewableDelegate<IUserConnection> Connected { get; }
    IReadOnlyDictionary<Guid, IUserConnection> Entries { get; }
    
    void OnConnected(Guid userId, string connectionId);
    void OnDisconnected(Guid userId);
    bool IsConnected(Guid userId);
}