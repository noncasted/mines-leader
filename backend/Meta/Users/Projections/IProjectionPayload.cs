using Shared;

namespace Meta.Users;

public interface IProjectionPayload 
{
    INetworkContext ToContext();
} 