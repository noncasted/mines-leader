using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Framework.ClientTelemetry.TestHost;

// A server that takes the socket and answers nothing: what a client meets when its meta hangs.
public static class TelemetryHostMuteSocket
{
    public const string Route = "/test-host/mute";

    public static void MapMuteSocket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(Route, async (HttpContext context) => {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest();

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TelemetryHostMuteSocket));
            await Listen(socket, logger, context.RequestAborted);

            return Results.Empty;
        });
    }

    private static async Task Listen(WebSocket socket, ILogger logger, CancellationToken cancellation)
    {
        var buffer = new byte[64 * 1024];

        try
        {
            while (socket.State == WebSocketState.Open)
                await socket.ReceiveAsync(buffer, cancellation);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            // The client went away without a close: the end of this socket either way, only noted.
            logger.LogDebug("[TestHost] The mute socket ended without a close: {Failure}", exception.GetType().Name);
        }
    }
}
