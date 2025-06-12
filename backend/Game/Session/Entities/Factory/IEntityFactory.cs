using Shared;

namespace Game;

public interface IEntityFactory
{
    IEntity Create(EntityContexts.CreateRequest request, IUser user);
    IEntity GetOrCreateService(EntityContexts.GetServiceRequest request);
}