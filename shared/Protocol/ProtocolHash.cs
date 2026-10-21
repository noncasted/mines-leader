namespace Shared
{
    // The protocol hash of this project, checked in both socket upgrades (see ProtocolVersion of the framework).
    public static class ProtocolHash
    {
        // First 16 hex chars of SHA-256 over backend/Tools/Tests/Protocol/protocol_schema.golden.txt (union tags,
        // members of every [MemoryPackable] shared type of the framework and of this project, enum values).
        // ProtocolSchemaTests fails with the new value when the schema changes: update it here together with the
        // golden file.
        public const string Value = "6b7cbd3ab1c1535f";
    }
}
