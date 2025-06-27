using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common;

public static class ContainerExtensions
{
    public class Registration
    {
        public required Type Type { get; init; }
        public required IHostApplicationBuilder Builder { get; init; }
    }

    public static Registration AddSingleton<TInterface, TImplementation>(
        this IHostApplicationBuilder builder)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        builder.Services.AddSingleton<TInterface, TImplementation>();

        return new Registration
        {
            Builder = builder,
            Type = typeof(TImplementation)
        };
    }

    public static Registration AddSingleton<TImplementation>(
        this IHostApplicationBuilder builder)
        where TImplementation : class
    {
        builder.Services.AddSingleton<TImplementation>();

        return new Registration
        {
            Builder = builder,
            Type = typeof(TImplementation)
        };
    }
    
    public static Registration As<T>(this Registration registration) where T : class
    {
        registration.Builder.Services.AddSingleton(sp => (T)sp.GetRequiredService(registration.Type));
        return registration;
    }
}