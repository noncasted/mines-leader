using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IGameModeConfig : IAddressableState<GameModeOptions>
{
}

public class GameModeConfigState(IOrleans orleans, IMessaging messaging) :
    AddressableState<GameModeOptions>(orleans, messaging),
    IGameModeConfig
{
}