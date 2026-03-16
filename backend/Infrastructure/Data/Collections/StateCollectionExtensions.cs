using Common.Extensions;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class StateCollectionExtensions  
{
    public static ContainerExtensions.Registration AddStateCollection<T, TKey, TState>(this IHostApplicationBuilder builder)
        where T : StateCollection<TKey, TState>
        where TKey : notnull
        where TState : class, new()
    {
        builder.Add<StateCollectionUtils<TKey, TState>>();
        
        return builder.Add<T>()
            .As<ILocalSetupCompleted>();

    }
}