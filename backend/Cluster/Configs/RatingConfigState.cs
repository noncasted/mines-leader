using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IRatingConfig : IAddressableState<RatingOptions>
{
}

public class RatingConfigState
    (IOrleans orleans, IMessaging messaging) : AddressableState<RatingOptions>(orleans, messaging), IRatingConfig
{
}
