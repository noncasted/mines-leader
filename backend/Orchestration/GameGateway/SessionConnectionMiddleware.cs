using System.Net.WebSockets;
using Common.Network;
using Infrastructure.Startup;
using Shared;

namespace GameGateway;

public class SessionConnectionMiddleware
{
    public SessionConnectionMiddleware(
        SessionGate gate,
        RequestDelegate next,
        IClusterParticipantContext participantContext,
        ILogger<SessionConnectionMiddleware> logger)
    {
        _gate = gate;
        _next = next;
        _participantContext = participantContext;
        _logger = logger;
    }

    // A client lost without a close (a domain reload in the middle of Play leaves its socket open in the editor
    // process, a crash, a dropped network) never ends the read loop by itself: the protocol ping finds it. A client
    // that does not answer a ping within KeepAliveTimeout is aborted, its user leaves the session as after a close and
    // the session left with no players is closed. A browser answers pings by itself (a hidden tab too); the Unity
    // client answers from its receive loop, which waits for the main thread after every message: a main thread
    // stalled for longer than interval + timeout (a paused editor) loses the session.
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(30);

    // A client that opened the socket must send its ticket at once (S4): a silent one does not hold the socket.
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly SessionGate _gate;
    private readonly RequestDelegate _next;
    private readonly IClusterParticipantContext _participantContext;
    private readonly ILogger<SessionConnectionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            await _next(context);
            return;
        }

        if (_participantContext.IsInitialized.Value == false)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Cluster participant is not initialized");
            return;
        }

        // Before the auth is read: a client of another protocol may not even parse the request the same way.
        if (ClientProtocolCheck.IsCurrent(context, ProtocolHash.Value) == false)
        {
            await ClientProtocolCheck.RejectOutdated(context, ProtocolHash.Value, _logger);
            return;
        }

        WebSocket? webSocket = null;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
            {
                KeepAliveInterval = KeepAliveInterval,
                KeepAliveTimeout = KeepAliveTimeout
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Game] Failed to accept WebSocket: {Connection}", context.Connection.Id);
            return;
        }

        if (webSocket == null)
        {
            _logger.LogError("[Game] AcceptWebSocketAsync returned null: {Connection}", context.Connection.Id);
            return;
        }

        var handle = new ConnectionOneTimeHandle(webSocket);
        SharedSessionAuth.Request auth;

        try
        {
            using var timeout = new CancellationTokenSource(HandshakeTimeout);
            auth = await handle.ReadRequest<SharedSessionAuth.Request>(timeout.Token);
        }
        catch (Exception e)
        {
            // A silent client (the timeout aborts the socket), a malformed or unexpected first frame.
            _logger.LogWarning(e, "[Game] WebSocket auth handshake failed: {Connection}", context.Connection.Id);
            handle.Dispose();
            await Close(webSocket, WebSocketCloseStatus.PolicyViolation, "Auth failed", context);
            return;
        }

        var entry = _gate.Enter(auth.Ticket);

        if (entry.Status != SessionAuthStatus.Ok)
        {
            // Expected refusals (a session closed after the search, an expired place) and attempts look the same
            // from here: the client gets the status, the log keeps who the ticket named (empty for a forged one).
            _logger.LogWarning("[Game] Connection rejected: {Status} {Connection} {UserId}",
                entry.Status,
                context.Connection.Id,
                entry.UserId);

            await Reject(handle, webSocket, entry.Status, context);
            return;
        }

        var session = entry.Session!;
        var userId = entry.UserId;

        _logger.LogInformation("[Game] User connected: {Connection} {UserId} {SessionId}",
            context.Connection.Id,
            userId,
            session.Id);

        try
        {
            await SessionGate.Join(
                session,
                userId,
                async () => {
                    await handle.SendResponse(new SharedSessionAuth.Response()
                    {
                        Status = SessionAuthStatus.Ok
                    });

                    handle.Dispose();
                },
                () => session.UserFactory.Create(session.Lifetime, userId, webSocket));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Game] Error during session connection handling: {Connection} {UserId}",
                context.Connection.Id,
                userId);
        }
        finally
        {
            _logger.LogInformation("[Game] User disconnected: {Connection} {UserId}",
                context.Connection.Id,
                userId);
        }
    }

    private async Task Reject(ConnectionOneTimeHandle handle, WebSocket webSocket, SessionAuthStatus status, HttpContext context)
    {
        try
        {
            await handle.SendResponse(new SharedSessionAuth.Response()
            {
                Status = status
            });
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
            _logger.LogWarning(e, "[Game] Failed to send the rejection: {Connection}", context.Connection.Id);
        }
        finally
        {
            handle.Dispose();
        }

        await Close(webSocket, WebSocketCloseStatus.PolicyViolation, status.ToString(), context);
    }

    private async Task Close(WebSocket webSocket, WebSocketCloseStatus status, string reason, HttpContext context)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        using var timeout = new CancellationTokenSource(CloseTimeout);

        try
        {
            await webSocket.CloseAsync(status, reason, timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
            _logger.LogWarning(e, "[Game] The client did not acknowledge the close: {Connection}", context.Connection.Id);
        }
    }
}
