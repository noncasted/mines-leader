using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Gateway;

public class BackendConnectionMiddleware
{
    public BackendConnectionMiddleware(
        RequestDelegate next,
        IUserConnectionEntryPoint entryPoint,
        ILogger<BackendConnectionMiddleware> logger)
    {
        _next = next;
        _entryPoint = entryPoint;
        _logger = logger;
    }

    private readonly RequestDelegate _next;
    private readonly IUserConnectionEntryPoint _entryPoint;
    private readonly ILogger<BackendConnectionMiddleware> _logger;
    private readonly IReadOnlyLifetime _lifetime = new Lifetime();

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            await _next(context);
            return;
        }

        using var activity = TraceExtensions.PlayerConnection.Start();

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handle = new ConnectionOneTimeHandle(webSocket);

        var auth = await handle.ReadRequest<SharedBackendSocketAuth.Request>();

        _logger.LogInformation("[Backend] [Gateway] User connected: {Connection} {UserId}",
            context.Connection.Id,
            auth
        );

        var completion = new TaskCompletionSource();

        var response = new SharedBackendSocketAuth.Response()
        {
            IsSuccess = true
        };

        await handle.SendResponse(response);
        handle.Dispose();

        var connection = new Connection(webSocket, _lifetime, _logger);

        var userSession = new UserSession
        {
            UserId = auth.UserId,
            Connection = connection
        };

        connection.Run().NoAwait();

        await _entryPoint.OnConnected(userSession);

        userSession.Lifetime.Listen(() => completion.TrySetResult());
        activity.Stop();

        await completion.Task;

        _logger.LogInformation("[Game] [Gateway] User disconnected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId
        );
    }
}

public static class BackendMiddlewareExtensions
{
    public static IApplicationBuilder AddBackendMiddleware(this IApplicationBuilder app)
    {
        app.UseCors(x => x
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials()
        );

        app.UseWebSockets();
        app.UseMiddleware<BackendConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}