using Shared;

namespace Game.Session
{
    // What the session does with the connection of a user: the transport is the host's (a WebSocket of the backend,
    // a data channel or a loopback of a host client).
    public interface IUserConnection
    {
        void Send(INetworkContext context);
        void Respond(INetworkContext context, int requestId);

        // The session drops the connection (a stale one of a user that came anew): its end removes the user.
        void Disconnect();
    }
}
