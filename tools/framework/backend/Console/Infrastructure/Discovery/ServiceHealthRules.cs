using Cluster.Discovery;

namespace Console;

// The health of a service by the age of its last heartbeat, and its colours: one rule for the Discovery page and the cluster widget.
public static class ServiceHealthRules
{
    private static readonly TimeSpan _healthyAge = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan _warningAge = TimeSpan.FromSeconds(60);

    public static ServiceHealth Of(IServiceOverview service, DateTime now)
    {
        var age = now - service.UpdateTime;

        if (age < _healthyAge)
            return ServiceHealth.Healthy;

        if (age < _warningAge)
            return ServiceHealth.Warning;

        return ServiceHealth.Stale;
    }

    // A role is as healthy as its worst service; a role without services is inactive.
    public static ServiceHealth OfRole(IReadOnlyList<IServiceOverview> services, DateTime now)
    {
        if (services.Count == 0)
            return ServiceHealth.Inactive;

        return services.Any(service => Of(service, now) != ServiceHealth.Healthy) ? ServiceHealth.Warning : ServiceHealth.Healthy;
    }

    public static string Background(ServiceHealth health) => health switch
    {
        ServiceHealth.Healthy => "bg-green-500/15",
        ServiceHealth.Warning => "bg-amber-500/15",
        ServiceHealth.Stale => "bg-red-500/15",
        ServiceHealth.Inactive => "bg-muted",
        _ => throw new ArgumentOutOfRangeException(nameof(health), health, null)
    };

    public static string Text(ServiceHealth health) => health switch
    {
        ServiceHealth.Healthy => "text-green-400",
        ServiceHealth.Warning => "text-amber-400",
        ServiceHealth.Stale => "text-red-400",
        ServiceHealth.Inactive => "text-muted-foreground",
        _ => throw new ArgumentOutOfRangeException(nameof(health), health, null)
    };

    public static string Dot(ServiceHealth health) => health switch
    {
        ServiceHealth.Healthy => "bg-green-400",
        ServiceHealth.Warning => "bg-amber-400",
        ServiceHealth.Stale => "bg-red-400",
        ServiceHealth.Inactive => "bg-muted-foreground",
        _ => throw new ArgumentOutOfRangeException(nameof(health), health, null)
    };
}
