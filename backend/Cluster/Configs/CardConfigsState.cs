using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface ICardConfigs : IAddressableState<CardConfigOptions>
{
}

public class CardConfigsState(IOrleans orleans, IMessaging messaging) :
    AddressableState<CardConfigOptions>(orleans, messaging),
    ICardConfigs
{
}