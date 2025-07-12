using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Gateway;

public class SessionConnectionMiddleware
{
    public SessionConnectionMiddleware(
        ISessionsCollection sessionsCollection,
        RequestDelegate next,
        ILogger<SessionConnectionMiddleware> logger)
    {
        _sessionsCollection = sessionsCollection;
        _next = next;
        _logger = logger;
    }

    private readonly ISessionsCollection _sessionsCollection;
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionConnectionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            await _next(context);
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handle = new ConnectionOneTimeHandle(webSocket);

        var auth = await handle.ReadRequest<GameConnectionAuth.Request>();

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

        var response = new GameConnectionAuth.Response()
        {
            IsSuccess = true
        };

        await handle.SendResponse(response);
        handle.Dispose();
        
        await completion.Task;

        _logger.LogInformation("[Game] [Gateway] User disconnected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId);
    }
}