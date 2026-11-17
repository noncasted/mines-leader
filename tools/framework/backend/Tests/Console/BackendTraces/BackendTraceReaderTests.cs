using System.Net;
using Console;
using Console.Infrastructure.BackendTraces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using static Framework.Tests.Console.BackendTraces.OtlpTraceJson;

namespace Framework.Tests.Console.BackendTraces;

public class BackendTraceReaderTests
{
    private const string TraceId = "1f3d84b522c4413c10965c00928b3fee";

    // The registration of the console with the dashboard under a path, its HTTP answered by the handler.
    private static IBackendTraceSource Source(FakeDashboardHandler handler, TimeSpan? timeout = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddConsoleBackendTraces(new ConsoleBackendTraceOptions
        {
            BaseUrl = new Uri("http://dashboard.test/aspire"),
            ApiKey = "test-key",
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        });
        builder.Services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => handler));

        return builder.Build().Services.GetRequiredService<IBackendTraceSource>();
    }

    [Fact]
    public async Task ATrace_IsReadWithTheKey_BelowThePathOfTheDashboard()
    {
        var handler = FakeDashboardHandler.Status(HttpStatusCode.OK, Answer(Resource("meta", Span("a", null, 0, 10))));

        var result = await Source(handler).Read(TraceId, CancellationToken.None);

        Assert.Equal("span a", Assert.IsType<BackendTraceResult.Found>(result).Trace.Spans.Single().Name);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"http://dashboard.test/aspire/api/telemetry/traces/{TraceId}", request.RequestUri!.AbsoluteUri);
        Assert.Equal(["test-key"], request.Headers.GetValues("x-api-key"));
    }

    // The dashboard 13.5 answers an unknown id with 404 and "{}".
    [Fact]
    public async Task NotFound_IsATraceGoneFromTheDashboard()
    {
        Assert.IsType<BackendTraceResult.NotFound>(await Source(FakeDashboardHandler.Status(HttpStatusCode.NotFound)).Read(TraceId, CancellationToken.None));
    }

    [Fact]
    public async Task AnotherStatus_IsAFailure_WithTheStatus()
    {
        var unauthorized = await Source(FakeDashboardHandler.Status(HttpStatusCode.Unauthorized)).Read(TraceId, CancellationToken.None);
        var error = await Source(FakeDashboardHandler.Status(HttpStatusCode.InternalServerError)).Read(TraceId, CancellationToken.None);

        Assert.Contains("401", Assert.IsType<BackendTraceResult.Failed>(unauthorized).Message);
        Assert.Contains("500", Assert.IsType<BackendTraceResult.Failed>(error).Message);
    }

    [Fact]
    public async Task ANetworkFailure_IsAFailure()
    {
        var handler = new FakeDashboardHandler((_, _) => throw new HttpRequestException("Connection refused"));

        var result = await Source(handler).Read(TraceId, CancellationToken.None);

        Assert.Contains("Connection refused", Assert.IsType<BackendTraceResult.Failed>(result).Message);
    }

    [Fact]
    public async Task ATimeout_IsAFailure_ButTheEndOfTheCaller_Cancels()
    {
        var handler = new FakeDashboardHandler(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            throw new InvalidOperationException("unreachable");
        });
        var source = Source(handler, TimeSpan.FromMilliseconds(50));

        var result = await source.Read(TraceId, CancellationToken.None);
        Assert.Contains("not answered", Assert.IsType<BackendTraceResult.Failed>(result).Message);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.Read(TraceId, cancelled.Token));
    }

    [Fact]
    public void TheOptions_OfASection_NeedTheUrlAndTheKey()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AspireDashboard:Url"] = "http://localhost:21316",
            ["AspireDashboard:ApiKey"] = "key",
            ["Broken:Url"] = "http://localhost:21316"
        }).Build();

        var options = ConsoleBackendTraceOptions.From(configuration.GetSection("AspireDashboard"));

        Assert.Equal((new Uri("http://localhost:21316"), "key"), (options.BaseUrl, options.ApiKey));
        Assert.Throws<InvalidOperationException>(() => ConsoleBackendTraceOptions.From(configuration.GetSection("Broken")));
    }
}
