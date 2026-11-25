using System;
using Internal;

namespace Meta
{
    public interface IMetaReconnection
    {
        // The first connect of MetaLoop authorized userId on connection (a child of scopeLifetime): from now on a lost
        // socket is opened again on a new child of scopeLifetime.
        void OnConnected(IReadOnlyLifetime scopeLifetime, ILifetime connection, Guid userId);
    }
}
