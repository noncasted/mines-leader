using System.Threading.Tasks;
using MemoryPack;

namespace Shared
{
    // The answer to a request that has no data to return. A request refused by the rules of the game (not an error of
    // the request) is answered with Fail: HasError and Message tell the caller why. A request that failed on the
    // other side (the handler threw, no handler) is answered with ErrorResponse instead.
    [MemoryPackable]
#if UNITY_5_3_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public partial class EmptyResponse : INetworkContext
    {
        [MemoryPackIgnore] public static readonly EmptyResponse Ok = new();

        [MemoryPackIgnore] public static readonly EmptyResponse Failed = new()
        {
            HasError = true
        };

        public bool HasError { get; set; }
        public string Message { get; set; }

        public static EmptyResponse Fail(string error)
        {
            return new EmptyResponse { HasError = true, Message = error };
        }
    }

    public static class EmptyResponseExtensions
    {
        public static async Task<INetworkContext> FromResult(this Task task)
        {
            await task;
            return EmptyResponse.Ok;
        }
    }
}
