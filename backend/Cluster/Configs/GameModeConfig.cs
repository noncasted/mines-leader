using Infrastructure;
using Shared;

namespace Cluster.Configs;

public interface IGameModeConfig : IRawAddressableState<GameModeOptions>
{
}

[GenerateSerializer]
public class GameModeConfigState : RawAddressableState
{
}

public class GameModeConfigView(IOrleans orleans, IMessaging messaging) :
    RawAddressableStateView<GameModeOptions, GameModeConfigState>(orleans, messaging),
    IGameModeConfig
{
    public override string Name => "config-game-modes";
}