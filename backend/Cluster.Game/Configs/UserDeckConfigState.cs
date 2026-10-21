using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IUserDeckConfig : IAddressableState<UserDeckConfigOptions>
{
}

public class UserDeckConfigState(AddressableStateUtils utils) : AddressableState<UserDeckConfigOptions>(utils), IUserDeckConfig
{
}
