using Infrastructure.Discovery;

namespace Services;

public interface IServiceOverview
{
    public Guid Id { get; }
    public string Name { get; }
    public ServiceTag Tag { get; }
    public DateTime UpdateTime { get; }
}