using Infrastructure.Discovery;

namespace Services;

public interface IServiceOverview
{
    public Guid Id { get; }
    public string Name { get; }
    public ServiceTag Tag { get; }
    public DateTime UpdateTime { get; }
}

[GenerateSerializer]
public class ServiceOverview : IServiceOverview
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required string Name { get; init; }
    [Id(2)] public required ServiceTag Tag { get; init; }
    [Id(3)] public required DateTime UpdateTime { get; init; }
}

[GenerateSerializer]
public class GameServerOverview : IServiceOverview
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required string Name { get; init; }
    [Id(2)] public required ServiceTag Tag { get; init; }
    [Id(3)] public required DateTime UpdateTime { get; init; }
    [Id(4)] public required string Url { get; init; }
}