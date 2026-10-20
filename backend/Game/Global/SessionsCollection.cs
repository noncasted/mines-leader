using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Extensions;
using Common.Reactive;
using Game.Session;
using Shared;

namespace Game.Global;

public interface ISessionsCollection
{
    IReadOnlyDictionary<Guid, ISession> Entries { get; }

    void Add(ISession session, GameMatchType? gameMode = null);

    // False for a session that is not in the collection (already closed or never created here).
    bool TryGet(Guid id, [MaybeNullWhen(false)] out ISession session);
}

public class SessionsCollection : ISessionsCollection
{
    public SessionsCollection(IDeploymentState<LiveMatchesData> liveData)
    {
        _liveData = liveData;
    }

    private readonly IDeploymentState<LiveMatchesData> _liveData;
    // Read by the gateway middleware, the search and HTTP endpoints, modified by session creation and by session queues.
    private readonly ConcurrentDictionary<Guid, ISession> _entries = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _createdAt = new();
    private readonly ConcurrentDictionary<Guid, GameMatchType?> _gameModes = new();

    // Snapshots of session users, replaced from the queue of the owning session: live data is pushed
    // from any thread and must not enumerate the user list of a foreign session.
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<Guid>> _userIds = new();

    public IReadOnlyDictionary<Guid, ISession> Entries => _entries;

    public void Add(ISession session, GameMatchType? gameMode = null)
    {
        if (_entries.TryAdd(session.Id, session) == false)
            throw new InvalidOperationException($"Session {session.Id} is already added");

        _createdAt[session.Id] = DateTime.UtcNow;
        _gameModes[session.Id] = gameMode;
        _userIds[session.Id] = [];

        // The list lifetime of a user ends after the user is removed from the list.
        session.Users.View(session.Lifetime, (userLifetime, _) => {
            OnUsersChanged();
            userLifetime.Listen(OnUsersChanged);
        });

        session.Lifetime.Listen(() => {
            _entries.TryRemove(session.Id, out _);
            _createdAt.TryRemove(session.Id, out _);
            _gameModes.TryRemove(session.Id, out _);
            _userIds.TryRemove(session.Id, out _);
            PushLiveData();
        });

        PushLiveData();

        void OnUsersChanged()
        {
            if (session.Lifetime.IsTerminated == true)
                return;

            _userIds[session.Id] = session.Users.Select(user => user.Id).ToList();
            PushLiveData();
        }
    }

    public bool TryGet(Guid id, [MaybeNullWhen(false)] out ISession session)
    {
        return _entries.TryGetValue(id, out session);
    }

    private void PushLiveData()
    {
        _liveData.SetValue(new LiveMatchesData
        {
            Matches = _entries.Values.Select(s => {
                var userIds = _userIds.GetValueOrDefault(s.Id, []);

                return new LiveMatchEntry
                {
                    Id = s.Id,
                    Type = s.Type.ToString(),
                    GameMode = _gameModes.GetValueOrDefault(s.Id)?.ToString() ?? string.Empty,
                    PlayerCount = userIds.Count,
                    Player1Id = userIds.ElementAtOrDefault(0),
                    Player2Id = userIds.ElementAtOrDefault(1),
                    CreatedAt = _createdAt.GetValueOrDefault(s.Id, DateTime.UtcNow)
                };
            }).ToList()
        }).NoAwait();
    }
}