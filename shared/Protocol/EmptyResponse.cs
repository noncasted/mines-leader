using System;
using System.Threading.Tasks;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class EmptyResponse : INetworkContext
    {
        public bool HasError { get; set; }
        public string Message { get; set; }

        [MemoryPackIgnore] public static readonly EmptyResponse Ok = new();

        [MemoryPackIgnore] public static readonly EmptyResponse Failed = new()
        {
            HasError = true
        };

        public static EmptyResponse Fail(string error)
        {
            return new EmptyResponse() { HasError = true, Message = error };
        }
    }

    public static class EmptyResponseExtensions
    {
        public static async Task<INetworkContext> FromResult(this Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                return EmptyResponse.Failed;
            }
            
            return EmptyResponse.Ok;
        }
    }
}