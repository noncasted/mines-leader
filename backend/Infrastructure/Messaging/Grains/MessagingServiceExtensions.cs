using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public static class MessagingServiceExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedSingleton<IMessagingClient, MessagingClient>();
        
        builder.Services.AddTransient<MessagingObserversCollection>(sp =>
            new MessagingObserversCollection(
                sp.GetRequiredService<ILogger<MessagingObserversCollection>>(),
                MessagingOptions.ObserverTimeout));

        return builder;
    }
}