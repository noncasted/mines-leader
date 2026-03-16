using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IBotConfig : IRawAddressableState<BotConfigOptions>
{
}

[GenerateSerializer]
public class BotConfigState : RawAddressableState
{
}

public class BotConfigView(IOrleans orleans, IMessaging messaging)
    : RawAddressableStateView<BotConfigOptions, BotConfigState>(orleans, messaging), IBotConfig
{
    public override string Name => "config-bot";
}