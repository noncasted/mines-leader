using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IGameModeConfig : IAddressableState<GameModeOptions>
{
}

public class GameModeConfigState(AddressableStateUtils utils) : AddressableState<GameModeOptions>(utils), IGameModeConfig
{
}