using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Backend
{
    public interface IBackendProjectionHub
    {
        UniTask Start(IReadOnlyLifetime lifetime, Guid userId);
        void AddListener<T>(IBackendProjection<T> projection) where T : INetworkContext;
    }
}