namespace Infrastructure.Discovery;

[GenerateSerializer]
public class ServerOverview
{
    [Id(0)]
    public required Guid ClientId { get; init; }
    
    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Url { get; init; }

    [Id(3)]
    public required DateTime UpdateTime { get; init; }
}