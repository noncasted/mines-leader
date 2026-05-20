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
    ISession Get(Guid id);
}

public class SessionsCollection : ISessionsCollection
{
    public SessionsCollection(IDeploymentState<LiveMatchesData> liveData)
    {
        _liveData = liveData;
    }

    private readonly IDeploymentState<LiveMatchesData> _liveData;
    private readonly Dictionary<Guid, ISession> _entries = new();
    private readonly Dictionary<Guid, DateTime> _createdAt = new();
    private readonly Dictionary<Guid, GameMatchType?> _gameModes = new();

    public IReadOnlyDictionary<Guid, ISession> Entries => _entries;

    public void Add(ISession session, GameMatchType? gameMode = null)
    {
        _entries.Add(session.Id, session);
        _createdAt.Add(session.Id, DateTime.UtcNow);
        _gameModes.Add(session.Id, gameMode);

        session.Users.View(session.Lifetime, user => {
            PushLiveData();
            user.Lifetime.Listen(PushLiveData);
        });

        session.Lifetime.Listen(() => {
            _entries.Remove(session.Id);
            _createdAt.Remove(session.Id);
            _gameModes.Remove(session.Id);
            PushLiveData();
        });

        PushLiveData();
    }

    public ISession Get(Guid id)
    {
        return _entries[id];
    }

    private void PushLiveData()
    {
        _liveData.SetValue(new LiveMatchesData
        {
            Matches = _entries.Values.Select(s => {
                var users = s.Users.ToList();
                var player1 = users.ElementAtOrDefault(0);
                var player2 = users.ElementAtOrDefault(1);

                return new LiveMatchEntry
                {
                    Id = s.Id,
                    Type = s.Type.ToString(),
                    GameMode = _gameModes.GetValueOrDefault(s.Id)?.ToString() ?? string.Empty,
                    PlayerCount = users.Count,
                    Player1Id = player1?.Id ?? Guid.Empty,
                    Player2Id = player2?.Id ?? Guid.Empty,
                    CreatedAt = _createdAt.GetValueOrDefault(s.Id, DateTime.UtcNow)
                };
            }).ToList()
        }).NoAwait();
    }
}