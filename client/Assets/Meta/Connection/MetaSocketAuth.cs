using Internal;
using Shared;

namespace Meta
{
    // The credentials of the meta socket (S3), the first frame of every connect: the saved guest of this backend
    // environment or nothing (a new guest).
    public static class MetaSocketAuth
    {
        public static SharedBackendSocketAuth.Request CreateRequest(IAuthentication authentication, ILog log)
        {
            var guest = authentication.LoadGuest();

            if (guest.HasValue == true)
            {
                return new SharedBackendSocketAuth.Request
                {
                    GuestId = guest.Value.Id,
                    GuestSecret = guest.Value.Secret
                };
            }

            // A user of a build before the guest secret is not entered by its id any more (no migration): the player
            // starts as a new guest, the old account stays on the server unreachable.
            var legacyUserId = authentication.LoadLegacyUserId();

            if (legacyUserId.HasValue == true)
                log.Warning("The saved user {UserId} has no guest secret and cannot be entered: a new guest is created", legacyUserId.Value);

            return new SharedBackendSocketAuth.Request();
        }
    }
}
