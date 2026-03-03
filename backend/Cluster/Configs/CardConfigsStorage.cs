using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface ICardConfigs : IRawAddressableState<CardConfigOptions>
{
}

public class CardConfigsView(IOrleans orleans, IMessaging messaging) :
    RawAddressableStateView<CardConfigOptions>(orleans, messaging),
    ICardConfigs
{
    public override string Name => "config-cards";
}