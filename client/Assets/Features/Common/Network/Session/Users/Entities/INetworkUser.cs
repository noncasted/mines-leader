using System;
using System.Collections.Generic;
using Internal;

namespace Common.Network
{
    public interface INetworkUser
    {
        int Index { get; }
        bool IsLocal { get; }
        Guid BackendId { get; }
        IReadOnlyDictionary<int, INetworkEntity> Entities { get; }
        IReadOnlyLifetime Lifetime { get; }
        
        void AddEntity(INetworkEntity entity);
        void DisposeRemote();
    }
}