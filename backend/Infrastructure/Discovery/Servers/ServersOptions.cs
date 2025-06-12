namespace Infrastructure.Discovery;

public class ServersOptions
{
    public TimeSpan UpdateInterval { get; } = TimeSpan.FromSeconds(5);
    public TimeSpan CheckInterval { get; } = TimeSpan.FromMinutes(2);
    public TimeSpan TimeOut { get; } = TimeSpan.FromMinutes(3);
}