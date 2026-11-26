using Internal;

namespace Infrastructure;

public interface IServiceStarted
{
    Task OnServiceStarted(IReadOnlyLifetime lifetime);
}
