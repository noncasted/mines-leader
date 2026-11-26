using Microsoft.Extensions.DependencyInjection;

namespace Console.Infrastructure.BackendTraces;

// The registration shared by the sources: the source and its named HTTP client. A console has one source: a second one throws.
public static class BackendTraceRegistration
{
    public static void Add<TSource>(IServiceCollection services, string httpClientName, Uri baseUrl, TimeSpan timeout, Action<HttpClient> configure)
        where TSource : class, IBackendTraceSource
    {
        if (services.Any(service => service.ServiceType == typeof(IBackendTraceSource)))
            throw new InvalidOperationException($"The console already has a source of backend traces, {typeof(TSource).Name} is a second one");

        services.AddSingleton<IBackendTraceSource, TSource>();
        services.AddHttpClient(httpClientName, client =>
        {
            client.BaseAddress = WithTrailingSlash(baseUrl);
            client.Timeout = timeout;
            configure(client);
        });
    }

    // A store under a path: the relative route of its API goes below it only after a slash.
    private static Uri WithTrailingSlash(Uri url)
    {
        return url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
    }
}
