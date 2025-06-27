using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceLoop;

namespace Infrastructure.Messaging;

public static class MessagingServiceExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.AddSingleton<MessagingClient>()
            .As<IMessagingClient>()
            .AsOrleansLoopStage();
        
        builder.Services.AddTransient<MessagingObserversCollection>(sp =>
            new MessagingObserversCollection(
                sp.GetRequiredService<ILogger<MessagingObserversCollection>>(),
                MessagingOptions.ObserverTimeout));

        return builder;
    }
}