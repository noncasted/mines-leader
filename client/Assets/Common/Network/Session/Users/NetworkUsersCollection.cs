using System.Collections.Generic;

namespace Common.Network
{
    public interface INetworkUsersCollection
    {
        IReadOnlyDictionary<int, INetworkUser> Entries { get; }        
        INetworkUser Local { get; }
        
        void Add(INetworkUser user);
    }
    
    public class NetworkUsersCollection : INetworkUsersCollection
    {
        private readonly Dictionary<int, INetworkUser> _entries = new();

        private INetworkUser _local;
        
        public IReadOnlyDictionary<int, INetworkUser> Entries => _entries;
        public INetworkUser Local => _local;

        public void Add(INetworkUser user)
        {
            if (user.IsLocal == true)
                _local = user;
            
            _entries.Add(user.Index, user);
            user.Lifetime.Listen(() => _entries.Remove(user.Index));
        }
    }
}