using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IRatingConfig : IAddressableState<RatingOptions>
{
}

public class RatingConfigState(AddressableStateUtils utils) : AddressableState<RatingOptions>(utils), IRatingConfig
{
}