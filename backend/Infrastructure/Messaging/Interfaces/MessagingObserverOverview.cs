using Infrastructure.Discovery;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Infrastructure.Messaging;

[GenerateSerializer]
public class MessagingObserverOverview
{
    [Id(0)]
    public required Guid ServiceId { get; init; }
    
    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required ServiceTag Tag { get; init; }

}