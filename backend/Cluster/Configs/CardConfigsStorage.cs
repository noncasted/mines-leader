using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface ICardConfigs : IAddressableState<CardsConfigs>
{
}

public class CardConfigsView(IOrleans orleans, IMessaging messaging) :
    RawAddressableStateView<CardsConfigs>(orleans, messaging),
    ICardConfigs
{
    public override string Name => "config-cards";
}