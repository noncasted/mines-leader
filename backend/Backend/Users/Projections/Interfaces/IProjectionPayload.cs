using Shared;

namespace Backend.Users.Projections;

public interface IProjectionPayload 
{
    INetworkContext ToContext();
} 