using System;
using System.Threading.Tasks;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class EmptyResponse : INetworkContext
    {
        public bool HasError { get; set; }

        [MemoryPackIgnore] public static readonly EmptyResponse Ok = new();

        [MemoryPackIgnore] public static readonly EmptyResponse Failed = new()
        {
            HasError = true
        };
    }

    public static class EmptyResponseExtensions
    {
        public static async Task<INetworkContext> FromResult(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                return EmptyResponse.Failed;
            }
            
            return EmptyResponse.Ok;
        }
    }
}