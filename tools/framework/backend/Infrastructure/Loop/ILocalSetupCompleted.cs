using Internal;

namespace Infrastructure;

public interface ILocalSetupCompleted
{
    Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime);
}
