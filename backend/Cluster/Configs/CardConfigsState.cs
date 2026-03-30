using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface ICardConfigs : IAddressableState<CardConfigOptions>
{
}

public class CardConfigsState(AddressableStateUtils utils) : AddressableState<CardConfigOptions>(utils), ICardConfigs
{
}