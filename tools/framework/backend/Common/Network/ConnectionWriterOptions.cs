namespace Common.Network;

public class ConnectionWriterOptions
{
    // Messages waiting for the socket. A client that lets this many pile up is too slow and gets disconnected:
    // producers (session ticks, messaging callbacks) never wait for a socket.
    public int QueueCapacity { get; init; } = 4096;
}
