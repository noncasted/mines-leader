using Console.Infrastructure.BackendTraces;
using Console.Infrastructure.BackendTraces.AspireDashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Console;

public static class ConsoleAspireDashboardTraceExtensions
{
    // The backend traces of the console from the HTTP API of the Aspire dashboard (a local cluster): the startup page of a client
    // shows the trace of the authorization of its socket. One source per console: AddConsoleVictoriaTraces is the other one.
    public static IHostApplicationBuilder AddConsoleAspireDashboardTraces(this IHostApplicationBuilder builder, AspireDashboardTraceOptions options)
    {
        BackendTraceRegistration.Add<AspireDashboardTraceReader>(builder.Services, AspireDashboardTraceReader.HttpClientName, options.BaseUrl,
            options.Timeout, client => client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey));

        return builder;
    }

    // A section with Url and ApiKey; a missing value throws.
    public static IHostApplicationBuilder AddConsoleAspireDashboardTraces(this IHostApplicationBuilder builder, IConfigurationSection section)
    {
        return builder.AddConsoleAspireDashboardTraces(AspireDashboardTraceOptions.From(section));
    }
}
