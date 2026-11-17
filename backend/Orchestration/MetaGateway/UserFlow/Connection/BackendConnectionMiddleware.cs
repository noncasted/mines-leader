using System.Diagnostics;
using System.Net.WebSockets;
using Cluster.State;
using Common.Extensions;
using Common.Network;
using Internal;
using Infrastructure;
using Infrastructure.Startup;
using Shared;

namespace MetaGateway.UserFlow.Connection;

public class BackendConnectionMiddleware
{
    public BackendConnectionMiddleware(
        RequestDelegate next,
        MetaSocketAuth auth,
        IUserConnectionEntryPoint entryPoint,
        IClusterFeatures clusterFeatures,
        IClusterParticipantContext participantContext,
        ILogger<BackendConnectionMiddleware> logger)
    {
        _next = next;
        _auth = auth;
        _entryPoint = entryPoint;
        _clusterFeatures = clusterFeatures;
        _participantContext = participantContext;
        _logger = logger;
    }

    // S3: the credentials come in the first frame (SharedBackendSocketAuth.Request), sent by the client right after
    // the socket opens; a client that stays silent does not hold the socket.
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly RequestDelegate _next;
    private readonly MetaSocketAuth _auth;
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

        if (_clusterFeatures.IsAcceptingConnections == false)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Cluster is not accepting connections");
            return;
        }

        // Before the user is resolved: an outdated client must not create or touch a user.
        if (ClientProtocolCheck.IsCurrent(context, ProtocolHash.Value) == false)
        {
            await ClientProtocolCheck.RejectOutdated(context, ProtocolHash.Value, _logger);
            return;
        }

        WebSocket webSocket;

        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Meta] Failed to accept WebSocket: {Connection}", context.Connection.Id);
            return;
        }

        // The root span ends after OnConnected; End on the way out covers the refusals and the failures.
        var trace = MetaConnectTrace.Start();

        try
        {
            await Serve(context, webSocket, trace);
        }
        finally
        {
            trace.End();
        }
    }

    private async Task Serve(HttpContext context, WebSocket webSocket, MetaConnectTrace trace)
    {
        // A query user id (a client before S3) is not looked at: the user comes only from the first frame.
        var handle = new ConnectionOneTimeHandle(webSocket);
        MetaSocketAuthResult result;

        try
        {
            SharedBackendSocketAuth.Request request;

            try
            {
                using var timeout = new CancellationTokenSource(HandshakeTimeout);
                request = await trace.Phase("Meta read request",
                    _ => handle.ReadRequest<SharedBackendSocketAuth.Request>(timeout.Token));
            }
            catch (Exception e)
            {
                // A silent client (the timeout aborts the socket), a malformed or unexpected first frame.
                _logger.LogWarning(e, "[Meta] Auth handshake failed: {Connection}", context.Connection.Id);
                await Close(webSocket, WebSocketCloseStatus.PolicyViolation, "Auth failed", context);
                return;
            }

            try
            {
                result = await trace.Phase("Meta auth", phase => Authenticate(request, phase));
            }
            catch (Exception e)
            {
                // The cluster failed (a conflicted transaction, the database): not a refusal of the credentials, the
                // client may come again.
                _logger.LogError(e, "[Meta] Failed to resolve user: {Connection}", context.Connection.Id);
                await Close(webSocket, WebSocketCloseStatus.InternalServerError, "Failed to resolve user", context);
                return;
            }

            await trace.Phase("Meta send response", () => handle.SendResponse(result.Response));
        }
        finally
        {
            handle.Dispose();
        }

        if (result.User == null)
        {
            _logger.LogWarning("[Meta] Auth rejected: {Status} {Connection}", result.Response.Status, context.Connection.Id);
            trace.Fail($"Auth rejected: {result.Response.Status}");

            await Close(webSocket,
                (WebSocketCloseStatus)SharedBackendSocketAuth.RejectedCloseCode,
                SharedBackendSocketAuth.RejectedReason,
                context);

            return;
        }

        var resolvedUser = result.User;
        var userId = resolvedUser.Id;

        _logger.LogInformation("[Meta] User connected: {Connection} {UserId}", context.Connection.Id, userId);

        var completion = new TaskCompletionSource();
        var lifetime = new Lifetime();
        var connection = new Common.Network.Connection(webSocket, lifetime, _logger);

        var userSession = new UserSession
        {
            UserId = userId,
            Connection = connection,
            TraceId = trace.TraceId
        };

        try
        {
            await trace.Phase("Meta projections", async () => {
                // OnConnected advises the dispatcher on the reader before its first await: a run without a handler
                // throws.
                var connected = _entryPoint.OnConnected(userSession, resolvedUser.Projections);
                trace.Detached(() => connection.Run().NoAwait());
                await connected;
            });

            trace.End();
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

            // The middleware returns only after the socket is closed, otherwise the close frame is lost with it.
            await connection.Closed;

            _logger.LogInformation("[Meta] User disconnected: {Connection} {UserId}",
                context.Connection.Id,
                userId);
        }
    }

    private async Task<MetaSocketAuthResult> Authenticate(SharedBackendSocketAuth.Request request, Activity? phase)
    {
        phase?.SetTag("meta.auth.guest", request.GuestId == Guid.Empty ? "new" : "known");

        var result = await _auth.Authenticate(request);
        phase?.SetTag("meta.auth.status", result.Response.Status.ToString());

        if (result.User == null)
            phase?.SetStatus(ActivityStatusCode.Error, result.Response.Status.ToString());

        return result;
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
            _logger.LogWarning(e, "[Meta] The client did not acknowledge the close: {Connection}", context.Connection.Id);
        }
    }
}

public static class BackendMiddlewareExtensions
{
    public static IApplicationBuilder AddBackendMiddleware(this IApplicationBuilder app)
    {
        app.UseAllowedOrigins();
        app.UseMiddleware<BackendConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}