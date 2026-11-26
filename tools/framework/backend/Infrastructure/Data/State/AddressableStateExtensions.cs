using Common.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class AddressableStateExtensions
{
    public static ContainerExtensions.Registration AddAddressableState<T>(this IHostApplicationBuilder builder)
        where T : class, IOrleansStarted
    {
        builder.Services.TryAddSingleton<AddressableStateUtils>();

        return builder.Add<T>()
                      .As<IOrleansStarted>();
    }
}