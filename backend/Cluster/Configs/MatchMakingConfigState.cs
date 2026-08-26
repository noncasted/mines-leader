using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IMatchMakingConfig : IAddressableState<MatchMakingOptions>
{
}

public class MatchMakingConfigState(AddressableStateUtils utils)
    : AddressableState<MatchMakingOptions>(utils), IMatchMakingConfig
{
}
