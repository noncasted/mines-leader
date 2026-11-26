using Internal;

namespace Infrastructure;

public interface IOrleansStarted
{
    Task OnOrleansStarted(IReadOnlyLifetime lifetime);
}
