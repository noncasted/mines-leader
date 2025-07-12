using Shared;

namespace Backend.Users;

public interface IProjectionPayload 
{
    INetworkContext ToContext();
} 