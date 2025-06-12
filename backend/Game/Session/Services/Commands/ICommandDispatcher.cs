using Common;

namespace Game;

public interface ICommandDispatcher
{
    Task Run(IReadOnlyLifetime lifetime);
}
