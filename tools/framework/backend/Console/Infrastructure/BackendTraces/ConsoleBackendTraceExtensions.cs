using Console.Infrastructure.BackendTraces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console;

public static class ConsoleBackendTraceExtensions
{
    // The backend traces of the console from the HTTP API of the Aspire dashboard: the startup page of a client shows the trace of
    // the authorization of its socket. Without this call the page shows only the id.
    public static IHostApplicationBuilder AddConsoleBackendTraces(this IHostApplicationBuilder builder, ConsoleBackendTraceOptions options)
    {
        builder.Services.AddSingleton<IBackendTraceSource, BackendTraceReader>();
        builder.Services.AddHttpClient(BackendTraceReader.HttpClientName, client =>
        {
            client.BaseAddress = WithTrailingSlash(options.BaseUrl);
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
        });

        return builder;
    }

    // A section with Url and ApiKey; a missing value throws.
    public static IHostApplicationBuilder AddConsoleBackendTraces(this IHostApplicationBuilder builder, IConfigurationSection section)
    {
        return builder.AddConsoleBackendTraces(ConsoleBackendTraceOptions.From(section));
    }

    // A dashboard under a path: the relative route of the API goes below it only after a slash.
    private static Uri WithTrailingSlash(Uri url)
    {
        return url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
    }
}
