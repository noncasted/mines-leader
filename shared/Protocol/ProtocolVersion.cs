namespace Shared
{
    // The version of the wire protocol. The client sends Hash in the query of both socket upgrades (meta and game);
    // a server built from another protocol accepts the socket and closes it at once with ClientOutdatedCloseCode,
    // the client shows the "update" notice. Checked before anything is read, so it does not depend on the union tags.
    public static class ProtocolVersion
    {
        // First 16 hex chars of SHA-256 over backend/Tools/Tests/Protocol/protocol_schema.golden.txt (union tags,
        // members of every [MemoryPackable] shared type, enum values). ProtocolSchemaTests fails with the new value
        // when the schema changes: update it here together with the golden file.
        public const string Hash = "701d30859db55520";

        public const string QueryKey = "protocol";

        // An application close code (4000-4999 are free for applications by RFC 6455).
        public const int ClientOutdatedCloseCode = 4001;
        public const string ClientOutdatedReason = "client outdated";
    }
}
