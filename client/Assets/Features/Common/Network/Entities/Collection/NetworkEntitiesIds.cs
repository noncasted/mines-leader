namespace Common.Network
{
    public class NetworkEntitiesIds : INetworkEntityIds
    {
        public NetworkEntitiesIds(INetworkSession session)
        {
            _session = session;
        }
        
        private const int _entitiesPerUser = 10000;

        private readonly INetworkSession _session;

        private int _counter = 0;
        
        public int GetEntityId()
        {
            var userIndex = _session.LocalIndex();
            var offset = _entitiesPerUser * userIndex;
            
            _counter++;
            
            return offset + _counter;
        }
    }
}