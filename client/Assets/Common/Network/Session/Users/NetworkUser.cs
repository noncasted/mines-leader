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
    
    public class NetworkUser : INetworkUser
    {
        public NetworkUser(
            int index,
            bool isLocal,
            ILifetime lifetime,
            Guid backendId)
        {
            _lifetime = lifetime;
            BackendId = backendId;
            Index = index;
            IsLocal = isLocal;
        }

        private readonly ILifetime _lifetime;
        private readonly Dictionary<int, INetworkEntity> _entities = new();
        
        public int Index { get; }
        public bool IsLocal { get; }
        public Guid BackendId { get; }
        public IReadOnlyDictionary<int, INetworkEntity> Entities => _entities;
        public IReadOnlyLifetime Lifetime => _lifetime;

        public void AddEntity(INetworkEntity entity)
        {
            _entities.Add(entity.Id, entity);
            entity.Lifetime.Listen(() => _entities.Remove(entity.Id));
        }

        public void DisposeRemote()
        {
            _lifetime.Terminate();
        }
    }
}