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
        IUserCommandsDispatcher commandsDispatcher,
        ILogger<UserConnectionMiddleware> logger)
    {
        _next = next;
        _users = users;
        _commandsDispatcher = commandsDispatcher;
        _logger = logger;
    }

    private readonly RequestDelegate _next;
    private readonly IConnectedUsers _users;
    private readonly IUserCommandsDispatcher _commandsDispatcher;
    private readonly ILogger<UserConnectionMiddleware> _logger;
    private readonly IReadOnlyLifetime _lifetime = new Lifetime();

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

        var connection = new Connection(webSocket, _lifetime, _logger);
        
        var userSession = new UserSession
        {
            UserId = auth.UserId,
            Connection = connection
        };

        connection.Run().NoAwait();
        _commandsDispatcher.Run(userSession);        
        _users.Add(userSession);

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