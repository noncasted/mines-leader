using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface INetworkConnection
    {
        UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId);
    }
}