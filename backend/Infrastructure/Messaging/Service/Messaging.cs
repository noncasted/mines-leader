using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Service;

namespace Infrastructure.Messaging;

public class Messaging : IMessaging
{
    public Messaging(IMessageQueueClient queue, IMessagePipeClient pipe)
    {
        Queue = queue;
        Pipe = pipe;
    }

    public IMessageQueueClient Queue { get; }
    public IMessagePipeClient Pipe { get; }
}

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        
        services.AddSingleton<IMessaging, Messaging>();
        services.AddSingleton<IMessageQueueClient, MessageQueueClient>();
        services.AddSingleton<IMessagePipeClient, MessagePipeClient>();
        
        return builder;
    }
}