using System.Collections.Generic;
using Common.Network.Common;
using Internal;

namespace Common.Network
{
    public class NetworkServiceData
    {
        public NetworkServiceData(
            int id,
            string key,
            IReadOnlyDictionary<int, INetworkProperty> properties,
            INetworkEvents events,
            IReadOnlyLifetime sessionLifetime)
        {
            Id = id;
            Key = key;
            Properties = properties;
            Events = events;
            SessionLifetime = sessionLifetime;
        }

        public int Id { get; }
        public string Key { get; }
        public IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
        public INetworkEvents Events { get; }
        public IReadOnlyLifetime SessionLifetime { get; }
    }

    public interface INetworkService : INetworkObject
    {
        string Key { get; }
    }

    public class NetworkService : INetworkService
    {
        private NetworkServiceData _data;

        public int Id => _data.Id;
        public string Key => _data.Key;
        public IReadOnlyDictionary<int, INetworkProperty> Properties => _data.Properties;
        public INetworkEvents Events => _data.Events;

        public IReadOnlyLifetime Lifetime => _data.SessionLifetime;

        public void Start(NetworkServiceData data)
        {
            _data = data;
            OnStarted(Lifetime);
        }
        
        public virtual void OnStarted(IReadOnlyLifetime lifetime)
        {
        }
    }
}