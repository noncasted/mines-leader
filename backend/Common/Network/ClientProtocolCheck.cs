using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Common.Network;

/// <summary>
/// F3.2: a client built from another protocol (a cached web build after a deploy) sends another
/// <see cref="ProtocolVersion.Hash"/> in the upgrade query, or none at all (a build from before the check). It is
/// told to update before anything is read: the socket is accepted and closed with
/// <see cref="ProtocolVersion.ClientOutdatedCloseCode"/>. Refusing the upgrade with an HTTP status would not do -
/// a browser WebSocket does not see the status, only 1006.
/// </summary>
public static class ClientProtocolCheck
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    public static bool IsCurrent(HttpContext context)
    {
        return context.Request.Query[ProtocolVersion.QueryKey].ToString() == ProtocolVersion.Hash;
    }

    public static async Task RejectOutdated(HttpContext context, ILogger logger)
    {
        logger.LogWarning("Outdated client protocol {Received}, expected {Expected}: {Connection}",
            context.Request.Query[ProtocolVersion.QueryKey].ToString(),
            ProtocolVersion.Hash,
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
