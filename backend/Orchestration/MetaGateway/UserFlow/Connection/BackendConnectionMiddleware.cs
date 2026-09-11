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
        IUserFactory userFactory,
        IUserConnectionEntryPoint entryPoint,
        IClusterFeatures clusterFeatures,
        IClusterParticipantContext participantContext,
        ILogger<BackendConnectionMiddleware> logger)
    {
        _next = next;
        _userFactory = userFactory;
        _entryPoint = entryPoint;
        _clusterFeatures = clusterFeatures;
        _participantContext = participantContext;
        _logger = logger;
    }

    private readonly RequestDelegate _next;
    private readonly IUserFactory _userFactory;
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

        // Авторизация приезжает прямо в запросе на апгрейд: отдельного кадра с
        // хендшейком нет, поэтому и лишнего плеча до сервера тоже нет.
        if (TryReadUserId(context, out var requestedUserId) == false)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Malformed user id");
            return;
        }

        // Регистрация живёт здесь же: отдельный http-эндпоинт стоил клиенту второго
        // tls-соединения к тому же хосту ради одного guid.
        ResolvedUser resolvedUser;

        try
        {
            resolvedUser = await _userFactory.Resolve(requestedUserId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] Failed to resolve user: {Connection} {UserId}",
                context.Connection.Id,
                requestedUserId);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Failed to resolve user");
            return;
        }

        var userId = resolvedUser.Id;
        WebSocket? webSocket;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] Failed to accept WebSocket: {Connection}", context.Connection.Id);
            return;
        }

        _logger.LogInformation("[Meta] User connected: {Connection} {UserId}", context.Connection.Id, userId);

        var completion = new TaskCompletionSource();
        var lifetime = new Lifetime();
        var connection = new Common.Network.Connection(webSocket, lifetime, _logger);

        var userSession = new UserSession
        {
            UserId = userId,
            Connection = connection
        };

        try
        {
            connection.Run().NoAwait();
            await _entryPoint.OnConnected(userSession, resolvedUser.Projections);
            userSession.Lifetime.Listen(() => completion.TrySetResult());

            await completion.Task;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] Error during user connection handling: {Connection} {UserId}",
                context.Connection.Id,
                userId);
        }
        finally
        {
            lifetime.Terminate();

            _logger.LogInformation("[Meta] User disconnected: {Connection} {UserId}",
                context.Connection.Id,
                userId);
        }

    }

    private static bool TryReadUserId(HttpContext context, out Guid? userId)
    {
        userId = null;

        var raw = context.Request.Query[SharedBackendSocketAuth.UserIdQueryKey].ToString();

        if (string.IsNullOrEmpty(raw) == true)
            return true;

        if (Guid.TryParse(raw, out var parsed) == false)
            return false;

        userId = parsed;
        return true;
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