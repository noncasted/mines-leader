using System.Net.WebSockets;
using Common.Network;
using Game.Global;
using Infrastructure.Startup;
using Shared;

namespace GameGateway;

public class SessionConnectionMiddleware
{
    public SessionConnectionMiddleware(
        ISessionsCollection sessionsCollection,
        RequestDelegate next,
        IClusterParticipantContext participantContext,
        ILogger<SessionConnectionMiddleware> logger)
    {
        _sessionsCollection = sessionsCollection;
        _next = next;
        _participantContext = participantContext;
        _logger = logger;
    }

    private readonly ISessionsCollection _sessionsCollection;
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

        WebSocket? webSocket = null;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync();
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

        ConnectionOneTimeHandle? handle = null;
        SharedSessionAuth.Request auth;

        try
        {
            handle = new ConnectionOneTimeHandle(webSocket);
            auth = await handle.ReadRequest<SharedSessionAuth.Request>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Game] WebSocket auth handshake failed: {Connection}", context.Connection.Id);
            handle?.Dispose();

            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Auth failed", CancellationToken.None);
                }
                catch (Exception closeEx)
                {
                    _logger.LogWarning(closeEx, "[Game] Failed to close WebSocket after auth error: {Connection}", context.Connection.Id);
                }
            }

            return;
        }

        var session = _sessionsCollection.Get(auth.SessionId);

        _logger.LogInformation("[Game] User connected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId);

        var completion = new TaskCompletionSource();

        try
        {
            session.ExecutionQueue.Enqueue(() => {
                var user = session.UserFactory.Create(session.Lifetime, auth.UserId, webSocket);
                user.Lifetime.Listen(() => completion.TrySetResult());
            });

            var response = new SharedSessionAuth.Response()
            {
                IsSuccess = true
            };

            await handle.SendResponse(response);
            handle.Dispose();

            await completion.Task;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Game] Error during session connection handling: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId);
        }
        finally
        {
            _logger.LogInformation("[Game] User disconnected: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId);
        }
    }
}
