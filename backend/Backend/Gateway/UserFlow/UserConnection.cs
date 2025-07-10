using System.Net.WebSockets;
using Common;
using MemoryPack;
using Shared;

namespace Backend.Gateway;

public interface IUserConnection
{
    Guid UserId { get; }
    string ConnectionId { get; }
    IReadOnlyLifetime Lifetime { get; }
}

public class UserConnection : IUserConnection
{
    private readonly MemoryStream _writeBuffer = new();
    private readonly byte[] _readBuffer = new byte[1024];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public required Guid UserId { get; init; }
    public required ILifetime InternalLifetime { get; init; }
    public required WebSocket WebSocket { get; init; }

    public IReadOnlyLifetime Lifetime => InternalLifetime;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            var message = await WebSocket.ReceiveAsync(_readBuffer, lifetime.Token);

            if (message.MessageType != WebSocketMessageType.Close)
                throw new Exception("Unexpected message type received from WebSocket.");

            InternalLifetime.Terminate();
            await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", lifetime.Token);
        }
    }

    public async Task Send<T>(T context) where T : INetworkContext
    {
        await _lock.WaitAsync(Lifetime.Token);

        await MemoryPackSerializer.SerializeAsync(_writeBuffer, context, cancellationToken: Lifetime.Token);
        var sendBuffer = new ReadOnlyMemory<byte>(_writeBuffer.GetBuffer(), 0, (int)_writeBuffer.Position);
        await WebSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, Lifetime.Token);
        _writeBuffer.Position = 0;

        _lock.Release();
    }
}