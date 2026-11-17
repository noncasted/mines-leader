namespace Common.Network;

public class ConnectionOverflowException(int capacity)
    : Exception($"Connection send queue overflow: {capacity} messages are waiting for a slow client");
