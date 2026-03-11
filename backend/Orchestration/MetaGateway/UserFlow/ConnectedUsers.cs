using Common.Reactive;
using MetaGateway.UserFlow.Connection;

namespace MetaGateway.UserFlow;

public interface IConnectedUsers
{
    IViewableDelegate<IUserSession> Connected { get; }
    IReadOnlyDictionary<Guid, IUserSession> Entries { get; }

    void Add(IUserSession session);
    void Remove(IUserSession session);
    bool IsConnected(Guid userId);
}

public class ConnectedUsers : IConnectedUsers
{
    private readonly ViewableDelegate<IUserSession> _connected = new();
    private readonly Dictionary<Guid, IUserSession> _entries = new();

    public IViewableDelegate<IUserSession> Connected => _connected;
    public IReadOnlyDictionary<Guid, IUserSession> Entries => _entries;

    public void Add(IUserSession session)
    {
        _entries.Add(session.UserId, session);
        session.Lifetime.Listen(() => _entries.Remove(session.UserId));

        _connected.Invoke(session);
    }

    public void Remove(IUserSession session)
    {
        _entries.Remove(session.UserId);
    }

    public bool IsConnected(Guid userId)
    {
        return _entries.ContainsKey(userId);
    }
}