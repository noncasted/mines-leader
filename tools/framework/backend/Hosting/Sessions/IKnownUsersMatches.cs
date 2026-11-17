using Game.Session;

namespace Game.Hosting;

// A match for a known set of users (a solo run): their places are reserved right away (an entry needs a live
// reservation), the session starts once all of them have joined, the search never finds it. Implemented by the game.
public interface IKnownUsersMatches
{
    ISession Create(IReadOnlyList<Guid> userIds);
}
