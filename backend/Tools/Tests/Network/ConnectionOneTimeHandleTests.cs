using System.Net.WebSockets;
using Common.Network;
using FluentAssertions;
using Shared;
using Xunit;

namespace Tests.Network;

// S3 / S4: the handshake read of the gateways ends with the timeout token - a client that opens a socket and
// stays silent does not hold it (the gateways pass HandshakeTimeout, 10 s).
public class ConnectionOneTimeHandleTests
{
    [Fact]
    public async Task ReadRequest_SilentClient_EndsOnTheTimeout()
    {
        using var socket = WebSocket.CreateFromStream(new SilentStream(), new WebSocketCreationOptions { IsServer = true });
        using var handle = new ConnectionOneTimeHandle(socket);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var read = () => handle.ReadRequest<SharedSessionAuth.Request>(timeout.Token);

        await read.Should().ThrowAsync<OperationCanceledException>();
        socket.State.Should().Be(WebSocketState.Aborted);
    }

    // A connection that never sends a byte.
    private class SilentStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
