using System.Collections.Concurrent;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Extensions;
using Common.Reactive;
using MetaGateway.UserFlow.Connection;

namespace MetaGateway.UserFlow;

public interface IConnectedUsers
{
    IViewableDelegate<IUserSession> Connected { get; }
    IReadOnlyDictionary<Guid, IUserSession> Entries { get; }

    // Registers the session; returns the previous session of the same user that it replaced, or null.
    IUserSession? Add(IUserSession session);

    // Removes this exact session; a newer session of the same user stays.
    void Remove(IUserSession session);
    bool IsConnected(Guid userId);
}

// Thread safe: connections are added from request threads and removed from lifetime callbacks of any thread.
// Every change and its live data push run under one lock, so SetValue calls start in the order of the changes
// and the last pushed value is the current set of users.
public class ConnectedUsers : IConnectedUsers
{
    public ConnectedUsers(IDeploymentState<ConnectedUsersLiveData> liveData)
    {
        _liveData = liveData;
    }

    private readonly IDeploymentState<ConnectedUsersLiveData> _liveData;
    private readonly ViewableDelegate<IUserSession> _connected = new();
    private readonly ConcurrentDictionary<Guid, IUserSession> _entries = new();
    private readonly Lock _lock = new();

    public IViewableDelegate<IUserSession> Connected => _connected;
    public IReadOnlyDictionary<Guid, IUserSession> Entries => _entries;

    public IUserSession? Add(IUserSession session)
    {
        IUserSession? replaced;

        lock (_lock)
        {
            _entries.TryGetValue(session.UserId, out replaced);
            _entries[session.UserId] = session;
            PushLiveData();
        }

        // Outside the lock: an already terminated lifetime invokes the callback right away.
        session.Lifetime.Listen(() => Remove(session));

        _connected.Invoke(session);
        return replaced;
    }

    public void Remove(IUserSession session)
    {
        lock (_lock)
        {
            if (_entries.TryRemove(new KeyValuePair<Guid, IUserSession>(session.UserId, session)) == false)
                return;

            PushLiveData();
        }
    }

    public bool IsConnected(Guid userId)
    {
        return _entries.ContainsKey(userId);
    }

    private void PushLiveData()
    {
        _liveData.SetValue(new ConnectedUsersLiveData
        {
            Count = _entries.Count,
            Users = _entries.Keys.Select(id => new ConnectedUserEntry
            {
                UserId = id
            }).ToList()
        }).NoAwait();
    }
}