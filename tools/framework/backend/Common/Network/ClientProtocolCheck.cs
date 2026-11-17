using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common.Network;

// F3.2: a client built from another protocol (a cached web build after a deploy) sends another
// protocol hash in the upgrade query, or none at all (a build from before the check). It is
// told to update before anything is read: the socket is accepted and closed with
// ProtocolVersion.ClientOutdatedCloseCode. Refusing the upgrade with an HTTP status would not do -
// a browser WebSocket does not see the status, only 1006.
// The expected hash is the protocol hash of the project (its shared types plus the framework ones).
public static class ClientProtocolCheck
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    public static bool IsCurrent(HttpContext context, string expectedHash)
    {
        return context.Request.Query[ProtocolVersion.QueryKey].ToString() == expectedHash;
    }

    public static async Task RejectOutdated(HttpContext context, string expectedHash, ILogger logger)
    {
        logger.LogWarning("Outdated client protocol {Received}, expected {Expected}: {Connection}",
            context.Request.Query[ProtocolVersion.QueryKey].ToString(),
            expectedHash,
            context.Connection.Id);

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var timeout = new CancellationTokenSource(CloseTimeout);

        try
        {
            await socket.CloseAsync((WebSocketCloseStatus)ProtocolVersion.ClientOutdatedCloseCode,
                ProtocolVersion.ClientOutdatedReason,
                timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
            // The client is gone before it acknowledged the close: nothing is left to tell it.
            logger.LogWarning(e, "Outdated client did not acknowledge the close: {Connection}",
                context.Connection.Id);
        }
    }
}
