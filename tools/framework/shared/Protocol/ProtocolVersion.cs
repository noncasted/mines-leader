namespace Shared
{
    // The version check of the wire protocol. The client sends the protocol hash of its project in the query of both
    // socket upgrades (meta and game); a server built from another protocol accepts the socket and closes it at once
    // with ClientOutdatedCloseCode, the client shows the "update" notice. Checked before anything is read, so it does
    // not depend on the union tags. The hash itself belongs to the project: the protocol is the framework types plus
    // the game types, so each project keeps its hash next to its own shared types (ProtocolHash.Value).
    public static class ProtocolVersion
    {
        public const string QueryKey = "protocol";

        // An application close code (4000-4999 are free for applications by RFC 6455).
        public const int ClientOutdatedCloseCode = 4001;
        public const string ClientOutdatedReason = "client outdated";
    }
}
