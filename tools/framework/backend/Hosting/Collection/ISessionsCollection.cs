using System.Diagnostics.CodeAnalysis;
using Game.Session;

namespace Game.Hosting.Collection;

// The live sessions of this game server. Read by the search and by the handlers of the meta requests, modified by the
// factory of the game and by the session queues; every change goes to the live data of the cluster (LiveSessionsData).
public interface ISessionsCollection
{
    IReadOnlyDictionary<Guid, ISession> Entries { get; }

    void Add(ISession session);

    // The moment the session was added; false for a session that is not in the collection (already closed).
    bool TryGetCreatedAt(Guid id, out DateTime createdAt);

    bool TryGet(Guid id, [MaybeNullWhen(false)] out ISession session);
}
