using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tests.Security;

// A real Kestrel host on a free loopback port: the pipeline under test runs exactly as in the gateways.
public sealed class TestWebHost : IAsyncDisposable
{
    private TestWebHost(WebApplication app, Uri address)
    {
        _app = app;
        Address = address;
        Http = new HttpClient { BaseAddress = address };
    }

    private readonly WebApplication _app;

    public Uri Address { get; }
    public HttpClient Http { get; }

    public static WebApplicationBuilder CreateBuilder(string environment, params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));

        return builder;
    }

    public static async Task<TestWebHost> Start(WebApplication app)
    {
        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();
        return new TestWebHost(app, new Uri(addresses.Addresses.Single()));
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
