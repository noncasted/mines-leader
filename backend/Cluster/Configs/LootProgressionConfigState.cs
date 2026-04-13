using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface ILootProgressionConfig : IAddressableState<LootProgressionOptions>
{
}

public class LootProgressionConfigState(AddressableStateUtils utils)
    : AddressableState<LootProgressionOptions>(utils), ILootProgressionConfig
{
}
