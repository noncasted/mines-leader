namespace Shared
{
    public enum SessionAuthStatus
    {
        Ok = 0,

        // Not signed by the meta, malformed, or already used.
        InvalidTicket = 1,

        Expired = 2,

        // The user holds no live place in the session (never reserved, expired, already used by another connection).
        NoReservation = 3,

        // The session is over or unknown on this server.
        Closed = 4,
    }
}
