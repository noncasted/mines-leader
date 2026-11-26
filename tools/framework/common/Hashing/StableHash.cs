// In Framework.Common (no MemoryPack) under the namespace of the protocol it came from: the core hashes with it
// without referencing Framework.Shared.
namespace Shared
{
    // string.GetHashCode is randomized per process on CoreCLR: ids shared between processes (clients and the
    // backend) need a fixed hash.
    public static class StableHash
    {
        // FNV-1a over UTF-16 code units.
        public static int Get(string value)
        {
            unchecked
            {
                var hash = 2166136261;

                foreach (var symbol in value)
                {
                    hash ^= symbol;
                    hash *= 16777619;
                }

                return (int)hash;
            }
        }
    }
}
