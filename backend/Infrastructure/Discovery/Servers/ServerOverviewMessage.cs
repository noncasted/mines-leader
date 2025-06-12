using Infrastructure.Messaging;

namespace Infrastructure.Discovery;

[GenerateSerializer]
public class ServerOverviewMessage : IClusterMessage
{
    [Id(0)]
    public required ServerOverview Overview { get; init; }
}