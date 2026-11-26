using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Extensions;
using Game.Session;
using Internal;

namespace Game.Hosting.Collection;

public class SessionsCollection : ISessionsCollection
{
    public SessionsCollection(IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>> liveData, TimeProvider time)
    {
        _liveData = liveData;
        _time = time;
    }

    private readonly IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>> _liveData;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, ISession> _entries = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _createdAt = new();

    // Snapshots of session users, replaced from the queue of the owning session: live data is pushed
    // from any thread and must not enumerate the user list of a foreign session.
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<Guid>> _userIds = new();

    // The queues of many sessions push at once: a snapshot is built and handed over under one lock, so the slices
    // reach SetSlice in the order of their state (the latest call wins there) and an older one never comes last.
    // The three dictionaries change under it too: a session in the snapshot always has its moment and its users.
    private readonly Lock _pushLock = new();

    public IReadOnlyDictionary<Guid, ISession> Entries => _entries;

    public void Add(ISession session)
    {
        lock (_pushLock)
        {
            if (_entries.ContainsKey(session.Id) == true)
                throw new InvalidOperationException($"Session {session.Id} is already added");

            _createdAt[session.Id] = _time.GetUtcNow().UtcDateTime;
            _userIds[session.Id] = [];
            _entries[session.Id] = session;
        }

        // The list lifetime of a user ends after the user is removed from the list.
        session.Users.View(session.Lifetime, (userLifetime, _) => {
            OnUsersChanged();
            userLifetime.Listen(OnUsersChanged);
        });

        session.Lifetime.Listen(() => {
            lock (_pushLock)
            {
                _entries.TryRemove(session.Id, out _);
                _createdAt.TryRemove(session.Id, out _);
                _userIds.TryRemove(session.Id, out _);
                PushLiveDataLocked();
            }
        });

        lock (_pushLock)
            PushLiveDataLocked();

        void OnUsersChanged()
        {
            if (session.Lifetime.IsTerminated == true)
                return;

            // In the queue of the session: its own user list.
            var userIds = session.Users.Select(user => user.BackendId).ToList();

            lock (_pushLock)
            {
                // Removed meanwhile: its users are not live data any more.
                if (_entries.ContainsKey(session.Id) == false)
                    return;

                _userIds[session.Id] = userIds;
                PushLiveDataLocked();
            }
        }
    }

    public bool TryGetCreatedAt(Guid id, out DateTime createdAt)
    {
        return _createdAt.TryGetValue(id, out createdAt);
    }

    public bool TryGet(Guid id, [MaybeNullWhen(false)] out ISession session)
    {
        return _entries.TryGetValue(id, out session);
    }

    private void PushLiveDataLocked()
    {
        _liveData.SetSlice(_entries.Values.Select(s => new LiveSessionEntry
        {
            Id = s.Id,
            UserIds = _userIds[s.Id].ToList(),
            CreatedAt = _createdAt[s.Id]
        }).ToList()).NoAwait();
    }
}
