using Cluster.Monitoring;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
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
    public ConnectedUsers(IDynamicState<ConnectedUsersLiveData> liveData)
    {
        _liveData = liveData;
    }

    private readonly IDynamicState<ConnectedUsersLiveData> _liveData;
    private readonly ViewableDelegate<IUserSession> _connected = new();
    private readonly Dictionary<Guid, IUserSession> _entries = new();

    public IViewableDelegate<IUserSession> Connected => _connected;
    public IReadOnlyDictionary<Guid, IUserSession> Entries => _entries;

    public void Add(IUserSession session)
    {
        _entries.Add(session.UserId, session);
        session.Lifetime.Listen(() => {
            _entries.Remove(session.UserId);
            PushLiveData();
        });

        _connected.Invoke(session);
        PushLiveData();
    }

    public void Remove(IUserSession session)
    {
        _entries.Remove(session.UserId);
        PushLiveData();
    }

    public bool IsConnected(Guid userId)
    {
        return _entries.ContainsKey(userId);
    }

    private void PushLiveData()
    {
        _liveData.SetValue(new ConnectedUsersLiveData {
            Count = _entries.Count,
            Users = _entries.Keys.Select(id => new ConnectedUserEntry {
                UserId = id
            }).ToList()
        }).NoAwait();
    }
}