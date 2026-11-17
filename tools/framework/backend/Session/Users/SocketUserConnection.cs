using Common.Network;
using Shared;

namespace Game.Session;

public class SocketUserConnection : IUserConnection
{
    public SocketUserConnection(IConnection socket)
    {
        _socket = socket;
    }

    private readonly IConnection _socket;

    public void Send(INetworkContext context)
    {
        _socket.Writer.WriteOneWay(context);
    }

    public void Respond(INetworkContext context, int requestId)
    {
        _socket.Writer.WriteResponse(context, requestId);
    }

    public void Disconnect()
    {
        _socket.ForceDisconnect();
    }
}
