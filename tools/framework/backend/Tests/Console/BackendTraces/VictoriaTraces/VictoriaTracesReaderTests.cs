using System.Net;
using System.Text.Json;
using Console;
using Console.Infrastructure.BackendTraces;
using Console.Infrastructure.BackendTraces.VictoriaTraces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using static Framework.Tests.Console.BackendTraces.VictoriaTraces.JaegerTraceJson;

namespace Framework.Tests.Console.BackendTraces.VictoriaTraces;

public class VictoriaTracesReaderTests
{
    // The answer of victoria-traces v0.13.0 to an unknown id.
    private const string NotFoundAnswer = "{\"data\":[],\"errors\":[{\"code\":404,\"msg\":\"trace not found\"}],\"limit\": 0,\"offset\": 0,\"total\":0}";

    // The registration of the console with the store under a path, its HTTP answered by the handler.
    private static IBackendTraceSource Source(FakeTraceStoreHandler handler, TimeSpan? timeout = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddConsoleVictoriaTraces(new VictoriaTracesOptions
        {
            BaseUrl = new Uri("http://traces.test/victoria"),
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        });
        builder.Services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => handler));

        return builder.Build().Services.GetRequiredService<IBackendTraceSource>();
    }

    [Fact]
    public async Task ATrace_IsReadByTheJaegerApi_BelowThePathOfTheStore()
    {
        var handler = FakeTraceStoreHandler.Status(HttpStatusCode.OK, Answer(new Dictionary<string, string> { ["p1"] = "meta" }, Span("a", null, 0, 10)));

        var result = await Source(handler).Read(TraceId, CancellationToken.None);

        Assert.Equal("span a", Assert.IsType<BackendTraceResult.Found>(result).Trace.Spans.Single().Name);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"http://traces.test/victoria/select/jaeger/api/traces/{TraceId}", request.RequestUri!.AbsoluteUri);
        Assert.False(request.Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task NotFound_IsATraceNotIndexedYet_OrPastTheRetention()
    {
        var notFound = await Source(FakeTraceStoreHandler.Status(HttpStatusCode.NotFound, NotFoundAnswer)).Read(TraceId, CancellationToken.None);
        var noSpans = await Source(FakeTraceStoreHandler.Status(HttpStatusCode.OK, Answer([]))).Read(TraceId, CancellationToken.None);

        Assert.Contains("20-30 s after its last span", Assert.IsType<BackendTraceResult.NotFound>(notFound).Message);
        Assert.Contains("20-30 s after its last span", Assert.IsType<BackendTraceResult.NotFound>(noSpans).Message);
    }

    [Fact]
    public async Task AnotherStatus_IsAFailure_WithTheStatus()
    {
        var result = await Source(FakeTraceStoreHandler.Status(HttpStatusCode.BadGateway)).Read(TraceId, CancellationToken.None);

        Assert.Equal("VictoriaTraces answered 502 Bad Gateway", Assert.IsType<BackendTraceResult.Failed>(result).Message);
    }

    [Fact]
    public async Task ANetworkFailure_OrATimeout_IsAFailure()
    {
        var refused = new FakeTraceStoreHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var silent = new FakeTraceStoreHandler(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            throw new InvalidOperationException("unreachable");
        });

        var failed = await Source(refused).Read(TraceId, CancellationToken.None);
        var late = await Source(silent, TimeSpan.FromMilliseconds(50)).Read(TraceId, CancellationToken.None);

        Assert.Equal("VictoriaTraces is not reachable: Connection refused", Assert.IsType<BackendTraceResult.Failed>(failed).Message);
        Assert.Contains("VictoriaTraces has not answered", Assert.IsType<BackendTraceResult.Failed>(late).Message);
    }

    // A 200 of another shape is a broken contract with the store: it throws, it is no message of the page.
    [Fact]
    public async Task ABrokenAnswer_Throws()
    {
        await Assert.ThrowsAnyAsync<JsonException>(() => Source(FakeTraceStoreHandler.Status(HttpStatusCode.OK, "<html>")).Read(TraceId, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Source(FakeTraceStoreHandler.Status(HttpStatusCode.OK, "{}")).Read(TraceId, CancellationToken.None));
    }

    [Fact]
    public void TheOptions_OfASection_NeedTheUrl()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BackendTraces:Url"] = "http://victoriatraces:10428",
            ["Broken:Timeout"] = "00:00:05"
        }).Build();

        Assert.Equal(new Uri("http://victoriatraces:10428"), VictoriaTracesOptions.From(configuration.GetSection("BackendTraces")).BaseUrl);
        Assert.Throws<InvalidOperationException>(() => VictoriaTracesOptions.From(configuration.GetSection("Broken")));
    }
}
