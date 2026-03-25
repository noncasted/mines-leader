using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IBotConfig : IAddressableState<BotConfigOptions>
{
}

public class BotConfigState
    (IOrleans orleans, IMessaging messaging) : AddressableState<BotConfigOptions>(orleans, messaging), IBotConfig
{
}