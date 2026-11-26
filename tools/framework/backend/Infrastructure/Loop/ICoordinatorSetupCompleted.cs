using Internal;

namespace Infrastructure;

public interface ICoordinatorSetupCompleted
{
    Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime);
}
