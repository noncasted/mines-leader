using Common;
using Shared;

namespace Game;

public interface IEntity
{
    int Id { get; }
    IUser Owner { get; }
    IReadOnlyList<IEntityProperty> Properties { get; }
    IReadOnlyLifetime Lifetime { get; }

    void Destroy();
    INetworkContext GetUpdateContext();
}