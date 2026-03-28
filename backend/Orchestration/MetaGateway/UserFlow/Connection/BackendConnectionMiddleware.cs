using Cluster.State;
using Common.Extensions;
using Common.Network;
using Common.Reactive;
using Infrastructure;
using Meta.Users;
using Shared;

namespace MetaGateway.UserFlow.Connection;

public class BackendConnectionMiddleware
{
    public BackendConnectionMiddleware(
        RequestDelegate next,
        IOrleans orleans,
        IUserConnectionEntryPoint entryPoint,
        IClusterFeatures clusterFeatures,
        ILogger<BackendConnectionMiddleware> logger)
    {
        _next = next;
        _orleans = orleans;
        _entryPoint = entryPoint;
        _clusterFeatures = clusterFeatures;
        _logger = logger;
    }

    private readonly RequestDelegate _next;
    private readonly IOrleans _orleans;
    private readonly IUserConnectionEntryPoint _entryPoint;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly ILogger<BackendConnectionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest == false)
        {
            await _next(context);
            return;
        }

        if (_clusterFeatures.Value.AcceptingConnections == false)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Cluster is not accepting connections");
            return;
        }

        using var activity = TraceExtensions.PlayerConnection.Start();

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handle = new ConnectionOneTimeHandle(webSocket);

        var auth = await handle.ReadRequest<SharedBackendSocketAuth.Request>();

        _logger.LogInformation("[Backend] [Meta] User connected: {Connection} {UserId}",
            context.Connection.Id,
            auth
        );

        var completion = new TaskCompletionSource();
        
        var userHandle = _orleans.CreateUserHandle(auth.UserId);
        var isExists = await _orleans.Transactions.Run(() => userHandle.Auth.IsExists());

        if (isExists == false)
        {
            _logger.LogWarning("[Backend] [Meta] User connection failed - user does not exist: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId
            );

            await handle.SendResponse(new SharedBackendSocketAuth.Response()
                {
                    IsSuccess = false
                }
            );
            
            handle.Dispose();
            return;
        }

        await handle.SendResponse(new SharedBackendSocketAuth.Response()
            {
                IsSuccess = true
            }
        );
            
        handle.Dispose();
        var lifetime = new Lifetime();
        var connection = new Common.Network.Connection(webSocket, lifetime, _logger);

        var userSession = new UserSession
        {
            UserId = auth.UserId,
            Connection = connection
        };

        try
        {
            connection.Run().NoAwait();
            await _entryPoint.OnConnected(userSession);
            userSession.Lifetime.Listen(() => completion.TrySetResult());

            await completion.Task;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Game] [Meta] Error during user connection handling: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId
            );
        }
        finally
        {
            activity.Stop();
            lifetime.Terminate();

            _logger.LogInformation("[Game] [Meta] User disconnected: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId
            );
        }
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