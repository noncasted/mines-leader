using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IInGameAchievementConfig : IAddressableState<InGameAchievementOptions>
{
}

public class InGameAchievementConfigState(AddressableStateUtils utils)
    : AddressableState<InGameAchievementOptions>(utils), IInGameAchievementConfig
{
}
