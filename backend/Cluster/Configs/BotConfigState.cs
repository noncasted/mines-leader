using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IBotConfig : IAddressableState<BotConfigOptions>
{
}

public class BotConfigState(AddressableStateUtils utils) : AddressableState<BotConfigOptions>(utils), IBotConfig
{
}