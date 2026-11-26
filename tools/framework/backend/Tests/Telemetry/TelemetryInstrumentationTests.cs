using System.Diagnostics;
using FluentAssertions;
using Framework.Tests.Security;
using Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Framework.Tests.Telemetry;

// The healthchecks of the deploys poll every port every few seconds: their requests make no server span, the rest do.
public class TelemetryInstrumentationTests
{
    private const string WorkPath = "/telemetry-instrumentation-work";

    [Fact]
    public async Task HealthRequests_MakeNoSpan_OtherRequestsDo()
    {
        var spans = new List<string>();
        var builder = TestWebHost.CreateBuilder("Production");
        builder.Services.AddOpenTelemetry()
               .WithTracing(tracing => tracing.AddDefaultInstrumentation()
                                              .AddProcessor(new CollectingProcessor(spans)));

        var app = builder.Build();
        var paths = new[]
        {
            TelemetryInstrumentation.HealthEndpointPath,
            TelemetryInstrumentation.AlivenessEndpointPath,
            TelemetryInstrumentation.ReadyEndpointPath,
            WorkPath
        };

        foreach (var path in paths)
            app.MapGet(path, () => "ok");

        await using var host = await TestWebHost.Start(app);

        foreach (var path in paths)
            (await host.Http.GetStringAsync(path)).Should().Be("ok");

        // A server span ends after the response is written.
        var port = host.Address.Port;
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (Collected(spans, port).Contains(WorkPath) == false && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Collected(spans, port).Should().Equal(WorkPath);
    }

    [Theory]
    [InlineData("/health", true)]
    [InlineData("/alive", true)]
    [InlineData("/ready", true)]
    [InlineData("/ready/db", true)]
    [InlineData("/readyz", false)]
    [InlineData("/api/ready", false)]
    public void IsHealthCheck_MatchesTheSegments(string path, bool expected)
    {
        TelemetryInstrumentation.IsHealthCheck(path).Should().Be(expected);
    }

    // The spans of other tests' hosts share the static source of ASP.NET Core: only this port is taken.
    private static List<string> Collected(List<string> spans, int port)
    {
        lock (spans)
        {
            return spans.Where(span => span.StartsWith($"{port} ", StringComparison.Ordinal))
                        .Select(span => span[(span.IndexOf(' ') + 1)..])
                        .ToList();
        }
    }

    private class CollectingProcessor(List<string> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            if (activity.Kind != ActivityKind.Server)
                return;

            lock (spans)
                spans.Add($"{activity.GetTagItem("server.port")} {activity.GetTagItem("url.path")}");
        }
    }
}
