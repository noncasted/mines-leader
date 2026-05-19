using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IPlayerConfig : IAddressableState<PlayerConfigOptions>
{
}

public class PlayerConfigState(AddressableStateUtils utils)
    : AddressableState<PlayerConfigOptions>(utils), IPlayerConfig
{
}
