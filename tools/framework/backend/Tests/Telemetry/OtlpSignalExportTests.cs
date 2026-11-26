using FluentAssertions;
using Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using Xunit;

namespace Framework.Tests.Telemetry;

// The endpoint of each signal: the connection string of the AppHost, the variables of one signal (logs and traces to
// different receivers), the variables of all signals read by the exporter itself, or no export.
public class OtlpSignalExportTests
{
    [Fact]
    public void SignalEndpoint_UsedAsItIs()
    {
        var configuration = Configuration(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317",
            ["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] = "http://logs:9428/insert/opentelemetry/v1/logs",
            ["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"] = "http/protobuf"
        });

        var logs = OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Logs)!;
        var traces = OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Traces)!;

        logs.Endpoint.Should().Be(new Uri("http://logs:9428/insert/opentelemetry/v1/logs"));
        logs.Protocol.Should().Be(OtlpExportProtocol.HttpProtobuf);
        traces.Endpoint.Should().BeNull("the exporter reads OTEL_EXPORTER_OTLP_ENDPOINT itself");
    }

    [Fact]
    public void ConnectionString_BaseUrlWithThePathOfTheSignal()
    {
        var configuration = Configuration(new() { ["ConnectionStrings:otel"] = "http://dashboard:18890/" });

        var traces = OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Traces)!;

        traces.Endpoint.Should().Be(new Uri("http://dashboard:18890/v1/traces"));
        traces.Protocol.Should().Be(OtlpExportProtocol.HttpProtobuf);
    }

    [Fact]
    public void NoEndpoint_NoExport()
    {
        OtlpSignalExport.Resolve(Configuration(new()), OtlpSignalExport.Metrics).Should().BeNull();
    }

    [Fact]
    public void UnknownProtocol_Throws()
    {
        var configuration = Configuration(new()
        {
            ["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = "http://traces/v1/traces",
            ["OTEL_EXPORTER_OTLP_TRACES_PROTOCOL"] = "http/json"
        });

        var resolve = () => OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Traces);

        resolve.Should().Throw<InvalidOperationException>();
    }

    // The local Aspire path: the exporter of a host takes the variables of all signals from its configuration.
    [Fact]
    public void HostExporters_TakeTheEndpointOfAllSignalsAndTheOneOfASignal()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://dashboard:4317",
            ["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = "http://traces:10428/insert/opentelemetry/v1/traces"
        });
        builder.AddTelemetryDefaults();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>();

        options.Get(OtlpExport.OptionsName(OtlpSignalExport.Logs)).Endpoint
               .Should().Be(new Uri("http://dashboard:4317"));
        options.Get(OtlpExport.OptionsName(OtlpSignalExport.Traces)).Endpoint
               .Should().Be(new Uri("http://traces:10428/insert/opentelemetry/v1/traces"));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
