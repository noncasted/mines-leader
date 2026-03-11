using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IGameModeConfig : IRawAddressableState<GameModeOptions>
{
}

public class GameModeConfigView(IOrleans orleans, IMessaging messaging) :
    RawAddressableStateView<GameModeOptions>(orleans, messaging),
    IGameModeConfig
{
    public override string Name => "config-game-modes";
}