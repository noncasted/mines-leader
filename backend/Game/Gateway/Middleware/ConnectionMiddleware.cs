using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Gateway;

public class ConnectionMiddleware
{
    public ConnectionMiddleware(
        ISessionsCollection sessionsCollection,
        RequestDelegate next,
        ILogger<ConnectionMiddleware> logger)
    {
        _sessionsCollection = sessionsCollection;
        _next = next;
        _logger = logger;
    }

    private readonly ISessionsCollection _sessionsCollection;
    private readonly RequestDelegate _next;
    private readonly ILogger<ConnectionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            await _next(context);
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        
        var auth = await webSocket.ReadOnce<GameConnectionAuth.Request>();

        var session = _sessionsCollection.Get(auth.SessionId);

        _logger.LogInformation("[Game] [Gateway] User connected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId);

        var completion = new TaskCompletionSource();

        session.ExecutionQueue.Enqueue(() =>
        {
            var user = session.UserFactory.Create(session.Lifetime, auth.UserId, webSocket);
            user.Lifetime.Listen(() => completion.TrySetResult());
        });

        await webSocket.SendOnce(new GameConnectionAuth.Response()
        {
            IsSuccess = true
        });
        
        await completion.Task;

        _logger.LogInformation("[Game] [Gateway] User disconnected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId);
    }
}