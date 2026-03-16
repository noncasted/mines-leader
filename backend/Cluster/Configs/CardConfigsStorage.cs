using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface ICardConfigs : IRawAddressableState<CardConfigOptions>
{
}

[GenerateSerializer]
public class CardConfigState : RawAddressableState
{
}

public class CardConfigsView(IOrleans orleans, IMessaging messaging) :
    RawAddressableStateView<CardConfigOptions, CardConfigState>(orleans, messaging),
    ICardConfigs
{
    public override string Name => "config-cards";
}