using Game.Session;
using Internal;

namespace Game.Lobbies;

// The players count of a listing (LobbyListingReporter, MatchListingService): reported again on every user added or removed.
internal static class LobbyListingUsers
{
    public static void ViewChanges(this ISessionUsers users, IReadOnlyLifetime lifetime, Action report)
    {
        // The list lifetime of a user ends after the user is removed from the list - or with the service (the
        // lifetime given by View is bounded by the subscriber one): the end of the session reports nothing.
        users.View(lifetime, (userLifetime, _) => {
            report();

            userLifetime.Listen(() => {
                if (lifetime.IsTerminated == false)
                    report();
            });
        });
    }
}
