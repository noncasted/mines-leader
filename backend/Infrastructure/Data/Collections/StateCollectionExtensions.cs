using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class StateCollectionExtensions
{
    // One singleton per collection: T is the instance that loads (ILocalSetupCompleted), and the generic
    // IStateCollection<TKey, TState> resolves to that same instance - never to a second, never loaded copy.
    public static ContainerExtensions.Registration AddStateCollection<T, TKey, TState>(
        this IHostApplicationBuilder builder)
        where T : StateCollection<TKey, TState>
        where TKey : notnull
        where TState : class, IStateValue, new()
    {
        if (builder.Services.Any(d => d.ServiceType == typeof(T)))
            throw new InvalidOperationException($"State collection {typeof(T).Name} is already registered");

        builder.Services.TryAddSingleton<StateCollectionUtils<TKey, TState>>();

        return builder.Add<T>()
                      .As<ILocalSetupCompleted>()
                      .As<IStateCollection<TKey, TState>>();
    }
}
