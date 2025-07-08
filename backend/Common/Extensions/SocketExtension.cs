using System.Net.WebSockets;
using MemoryPack;
using Shared;

namespace Common;

public static class SocketExtension
{
    public static async Task<T> ReadOnce<T>(this WebSocket webSocket)
    {
        var buffer = new byte[1024 * 1024 * 4].AsMemory();
        var rawAuth = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        var payload = buffer[..rawAuth.Count];
        var context = MemoryPackSerializer.Deserialize<T>(payload.Span)!;

        return context;
    } 
    
    public static async Task SendOnce<T>(this WebSocket webSocket, T context)
    {
        var buffer = new MemoryStream();
        
        await MemoryPackSerializer.SerializeAsync(buffer, context);
        var sendBuffer = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);
        
        await webSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
    } 
}