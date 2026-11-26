namespace Game.Session;

// The answer of a session to a reservation (ISession.Reserve): the place, or why there is none.
public enum SessionReservation
{
    Reserved,

    // The lobby has started its match (ISessionAdmission.Seal); checked before Closed: a sealed lobby its users have
    // left is still a started one.
    Sealed,

    // No place can be taken any more; the lifetime is terminated a moment later, from the queue.
    Closed,

    // Every place is held by other users (SessionCreateOptions.MaxUsers).
    Full,

    // The stale connections of the user itself are all that holds the session: they are dropped and the session closes,
    // so for the caller it is a closing session, not a full one.
    HeldOnlyByOwnStale
}
