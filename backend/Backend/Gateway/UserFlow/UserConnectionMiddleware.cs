using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Gateway;

public class UserConnectionMiddleware
{
    public UserConnectionMiddleware(
        RequestDelegate next,
        IConnectedUsers users,
        ILogger<UserConnectionMiddleware> logger)
    {
        _next = next;
        _users = users;
        _logger = logger;
    }

    private readonly RequestDelegate _next;
    private readonly IConnectedUsers _users;
    private readonly ILogger<UserConnectionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            await _next(context);
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        
        var auth = await webSocket.ReadOnce<BackendConnectionAuth.Request>();

        _logger.LogInformation("[Backend] [Gateway] User connected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId);

        var completion = new TaskCompletionSource();

        await webSocket.SendOnce(new GameConnectionAuth.Response()
        {
            IsSuccess = true
        });
        
        
        var connection = new UserConnection
        {
            UserId = auth.UserId,
            InternalLifetime = new Lifetime(),
            WebSocket = null
        };
        
        _users.OnConnected(auth.UserId, context.Connection.Id);
        
        await completion.Task;

        _logger.LogInformation("[Game] [Gateway] User disconnected: {Connection} {UserId}",
            context.Connection.Id,
            auth.UserId);
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
            .AllowCredentials()); 
        
        app.UseWebSockets();
        app.UseMiddleware<UserConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}