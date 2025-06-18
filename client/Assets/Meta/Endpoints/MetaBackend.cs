using Global.Backend;
using Internal;

namespace Meta
{
    public interface IMetaBackend
    {
        IUser User { get; }
        IBackendClient Client { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
    
    public class MetaBackend : IMetaBackend
    {
        public MetaBackend(IUser user, IBackendClient client, IReadOnlyLifetime lifetime)
        {
            User = user;
            Client = client;
            this.Lifetime = lifetime;
        }

        public IUser User { get; }
        public IBackendClient Client { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}