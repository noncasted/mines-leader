using Microsoft.Extensions.Configuration;

namespace Aspire;

// Dev-only: Grafana over the local Postgres for the match metrics (tables match_metric_*), never deployed.
// Provisioning (the datasource and the dashboard "Match metrics") is in ContainersData/Grafana; the datasource reads
// the address and the credentials of Postgres from the environment set here. Off by "Grafana:Enabled" = false.
// Anonymous visitors view; admin / admin edits (a local container only).
// Persistent like Postgres, so it is in the same container network and reaches it by the name of the resource.
public static class GrafanaFactory
{
    private const string ImageTag = "13.2.5";

    public static void Create(IDistributedApplicationBuilder builder, IConfiguration configuration, DbUpstream db)
    {
        if (!configuration.GetSection("Grafana").GetValue<bool>("Enabled"))
            return;

        // Next to the Postgres (9632) and PgBouncer (9633) ports of this AppHost.
        var port = db.HostPort + 2;
        var provisioning = Path.Combine(builder.AppHostDirectory, "ContainersData/Grafana/provisioning");

        builder.AddContainer("grafana", "grafana/grafana", ImageTag)
               .WithHttpEndpoint(port: port, targetPort: 3000, name: "http", isProxied: false)
               .WithBindMount(provisioning, "/etc/grafana/provisioning", isReadOnly: true)
               .WithEnvironment("PG_HOST", db.Host)
               .WithEnvironment("PG_PORT", db.Port.ToString())
               .WithEnvironment("PG_DATABASE", db.Database)
               .WithEnvironment("PG_USER", db.User)
               .WithEnvironment("PG_PASSWORD", db.Password)
               .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
               .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Viewer")
               .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
               .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
               .WithEnvironment("GF_DASHBOARDS_DEFAULT_HOME_DASHBOARD_PATH",
                   "/etc/grafana/provisioning/dashboards/match_metrics.json")
               .WithEnvironment("GF_ANALYTICS_REPORTING_ENABLED", "false")
               .WithEnvironment("GF_ANALYTICS_CHECK_FOR_UPDATES", "false")
               .WithEnvironment("GF_NEWS_NEWS_FEED_ENABLED", "false")
               .WithLifetime(ContainerLifetime.Persistent)
               .WaitFor(db.PostgresResource);
    }
}
