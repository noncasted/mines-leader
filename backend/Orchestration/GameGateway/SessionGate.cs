using Common.Network;
using Game.Global;
using Game.Session;
using Orchestration;
using Shared;
using ISession = Game.Session.ISession;

namespace GameGateway;

public readonly record struct SessionGateResult(SessionAuthStatus Status, ISession? Session, Guid UserId);

/// <summary>
/// S4: who enters which session. The user and the session come only from a ticket the meta (or the game server, for a
/// rematch) has signed (<see cref="ISessionTicketVerifier"/>: signature, expiry, one use); the place - only from a
/// live reservation of that user in the session (<see cref="ISession.TryAccept"/>).
/// </summary>
public class SessionGate
{
    public SessionGate(ISessionsCollection sessions, ISessionTicketVerifier tickets)
    {
        _sessions = sessions;
        _tickets = tickets;
    }

    private readonly ISessionsCollection _sessions;
    private readonly ISessionTicketVerifier _tickets;

    public SessionGateResult Enter(string ticket)
    {
        var check = _tickets.Verify(ticket);

        switch (check.Check)
        {
            case SessionTicketCheck.Ok:
                break;
            case SessionTicketCheck.Invalid:
            case SessionTicketCheck.Replayed:
                return new SessionGateResult(SessionAuthStatus.InvalidTicket, null, check.Ticket.UserId);
            case SessionTicketCheck.Expired:
                return new SessionGateResult(SessionAuthStatus.Expired, null, check.Ticket.UserId);
            default:
                throw new ArgumentOutOfRangeException(nameof(check.Check), check.Check, null);
        }

        var userId = check.Ticket.UserId;

        // The session may have been closed between the result of the meta and this connection.
        if (_sessions.TryGet(check.Ticket.SessionId, out var session) == false || session.Lifetime.IsTerminated == true)
            return new SessionGateResult(SessionAuthStatus.Closed, null, userId);

        if (session.TryAccept(userId) == false)
            return new SessionGateResult(SessionAuthStatus.NoReservation, null, userId);

        return new SessionGateResult(SessionAuthStatus.Ok, session, userId);
    }

    // After Ok: the auth response goes out first and only then the user is created in the session queue. The created
    // user starts sending on the same socket at once (Connection.Run, LocalUpdate) and a WebSocket allows a single
    // sender at a time; the client also expects the auth response as the first frame. The returned task completes
    // when the user leaves the session (its lifetime ends).
    public static async Task Join(ISession session, Func<Task> sendAccepted, Func<IUser> createUser)
    {
        await sendAccepted();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        session.ExecutionQueue.Enqueue(() => {
            try
            {
                var user = createUser();
                user.Lifetime.Listen(() => completion.TrySetResult());
            }
            catch (Exception e)
            {
                // The connection must not wait forever for a user that was never created.
                completion.TrySetException(e);
                throw;
            }
        });

        await completion.Task;
    }
}

public static class SessionGateExtensions
{
    public static IHostApplicationBuilder AddSessionGate(this IHostApplicationBuilder builder)
    {
        builder.AddSessionTickets();
        builder.Services.AddSingleton<SessionGate>();
        return builder;
    }
}
