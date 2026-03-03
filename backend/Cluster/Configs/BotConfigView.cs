using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IBotConfig : IRawAddressableState<BotConfigOptions>
{
}

public class BotConfigView(IOrleans orleans, IMessaging messaging)
    : RawAddressableStateView<BotConfigOptions>(orleans, messaging), IBotConfig
{
    public override string Name => "config-bot";
}