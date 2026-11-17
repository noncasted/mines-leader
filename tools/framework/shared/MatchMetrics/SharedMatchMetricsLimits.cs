namespace Shared
{
    // The limits of the content of a stream, the same on both sides: the client throws on a write over them, the server
    // rejects a batch over them. The sizes of a batch are the server's alone.
    public static class SharedMatchMetricsLimits
    {
        public const int MaxNameLength = 64;
        public const int MaxTagLength = 32;
        public const int MaxTags = 4;
        public const int MaxFields = 8;
        public const int MaxSeries = 256;
    }
}
