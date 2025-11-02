using Microsoft.Extensions.DependencyInjection;

namespace Common.Extensions;

public static class ContainerExtensions
{
    public class Registration
    {
        public required Type Type { get; init; }
        public required IServiceCollection Collection { get; init; }
    }

    extension(IServiceCollection builder)
    {
        public Registration Add<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            builder.AddSingleton<TImplementation>();
            builder.AddSingleton<TInterface>(sp => sp.GetRequiredService<TImplementation>());

            return new Registration
            {
                Collection = builder,
                Type = typeof(TImplementation)
            };
        }

        public Registration Add<T>()
            where T : class
        {
            builder.AddSingleton<T>();

            return new Registration
            {
                Collection = builder,
                Type = typeof(T)
            };
        }
        
        public Registration Add<T>(T instance)
            where T : class
        {
            builder.AddSingleton(instance);

            return new Registration
            {
                Collection = builder,
                Type = typeof(T)
            };
        }

        public Registration Pass<T>(IServiceProvider services) where T : class
        {
            return builder.Add(services.GetRequiredService<T>());
        }
    }

    extension(Registration registration)
    {
        public Registration As<T>() where T : class
        {
            registration.Collection.AddSingleton(sp => (T)sp.GetRequiredService(registration.Type));
            return registration;
        }
    }
}