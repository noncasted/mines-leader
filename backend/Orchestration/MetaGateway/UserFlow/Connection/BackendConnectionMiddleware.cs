using System.Net.WebSockets;
using Cluster.State;
using Common.Extensions;
using Common.Network;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Startup;
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
        IClusterParticipantContext participantContext,
        ILogger<BackendConnectionMiddleware> logger)
    {
        _next = next;
        _orleans = orleans;
        _entryPoint = entryPoint;
        _clusterFeatures = clusterFeatures;
        _participantContext = participantContext;
        _logger = logger;
    }

    private readonly RequestDelegate _next;
    private readonly IOrleans _orleans;
    private readonly IUserConnectionEntryPoint _entryPoint;
    private readonly IClusterFeatures _clusterFeatures;
    private readonly IClusterParticipantContext _participantContext;
    private readonly ILogger<BackendConnectionMiddleware> _logger;

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

        if (_clusterFeatures.Value.AcceptingConnections == false)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Cluster is not accepting connections");
            return;
        }

        WebSocket? webSocket = null;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] Failed to accept WebSocket: {Connection}", context.Connection.Id);
            return;
        }

        if (webSocket == null)
        {
            _logger.LogError("[Meta] AcceptWebSocketAsync returned null: {Connection}", context.Connection.Id);
            return;
        }

        ConnectionOneTimeHandle? handle = null;
        SharedBackendSocketAuth.Request auth;

        try
        {
            handle = new ConnectionOneTimeHandle(webSocket);
            auth = await handle.ReadRequest<SharedBackendSocketAuth.Request>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] WebSocket auth handshake failed: {Connection}", context.Connection.Id);
            handle?.Dispose();

            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Auth failed", CancellationToken.None);
                }
                catch (Exception closeEx)
                {
                    _logger.LogWarning(closeEx, "[Meta] Failed to close WebSocket after auth error: {Connection}", context.Connection.Id);
                }
            }

            return;
        }

        _logger.LogInformation("[Meta] User connected: {Connection} {UserId}",
            context.Connection.Id,
            auth);

        var userHandle = _orleans.CreateUserHandle(auth.UserId);
        var isExists = await _orleans.Transactions.Run(() => userHandle.Auth.IsExists());

        if (isExists == false)
        {
            _logger.LogWarning("[Meta] User connection failed — user does not exist: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId);

            try
            {
                await handle.SendResponse(new SharedBackendSocketAuth.Response()
                {
                    IsSuccess = false
                });
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "[Meta] Failed to send auth rejection: {Connection} {UserId}",
                    context.Connection.Id,
                    auth.UserId);
            }

            handle.Dispose();
            return;
        }

        try
        {
            await handle.SendResponse(new SharedBackendSocketAuth.Response()
            {
                IsSuccess = true
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] Failed to send auth success: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId);
            handle.Dispose();
            return;
        }

        handle.Dispose();

        var completion = new TaskCompletionSource();
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
            _logger.LogError(e, "[Meta] Error during user connection handling: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId);
        }
        finally
        {
            lifetime.Terminate();

            _logger.LogInformation("[Meta] User disconnected: {Connection} {UserId}",
                context.Connection.Id,
                auth.UserId);
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
                         .AllowCredentials());

        app.UseWebSockets();
        app.UseMiddleware<BackendConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}
