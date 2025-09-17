using Infrastructure.Discovery;

namespace Services;

public interface IServiceOverview
{
    public Guid Id { get; }
    public string Name { get; }
    public ServiceTag Tag { get; }
    public DateTime UpdateTime { get; }
}

public class ServiceOverview
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ServiceTag Tag { get; init; }
    public required DateTime UpdateTime { get; init; }
}