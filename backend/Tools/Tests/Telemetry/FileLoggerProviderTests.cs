using FluentAssertions;
using Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orchestration;
using Xunit;

namespace Tests.Telemetry;

/// <summary>
/// F2.12: the file logger is configured through FileLoggerOptions and registered only in Development.
/// </summary>
public class FileLoggerProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"file-logger-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Fact]
    public void Writes_ToConfiguredDirectory_WithMinLevel()
    {
        var provider = new FileLoggerProvider("svc", new FileLoggerOptions
        {
            Directory = _directory,
            MinLevel = LogLevel.Information
        });

        var logger = provider.CreateLogger("Category");
        logger.LogDebug("hidden");
        logger.LogInformation("shown {Value}", 42);
        logger.LogError(new InvalidOperationException("boom"), "failed");

        provider.Dispose();

        provider.FilePath.Should().Be(Path.Combine(_directory, "svc.log"));
        var text = File.ReadAllText(provider.FilePath);

        text.Should().NotContain("hidden");
        text.Should().Contain("[INF] [Category] shown 42");
        text.Should().Contain("[ERR] [Category] failed");
        text.Should().Contain("System.InvalidOperationException: boom");
    }

    [Fact]
    public async Task ConcurrentLoggers_AllLinesWritten()
    {
        var provider = new FileLoggerProvider("concurrent", new FileLoggerOptions { Directory = _directory });

        await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() => {
            var logger = provider.CreateLogger($"Thread{thread}");

            for (var i = 0; i < 500; i++)
                logger.LogInformation("line {Thread} {Index}", thread, i);
        })));

        provider.Dispose();

        File.ReadAllLines(provider.FilePath).Should().HaveCount(8 * 500);
    }

    [Fact]
    public void Production_DoesNotRegisterFileLogger()
    {
        using var host = Build("Production", ("FileLogging:Directory", _directory));

        host.Services.GetServices<ILoggerProvider>().OfType<FileLoggerProvider>().Should().BeEmpty();
        Directory.Exists(_directory).Should().BeFalse();
    }

    [Fact]
    public void Development_RegistersFileLogger_FromOptions()
    {
        using var host = Build("Development", ("FileLogging:Directory", _directory), ("FileLogging:MinLevel", "Warning"));

        var provider = host.Services.GetServices<ILoggerProvider>().OfType<FileLoggerProvider>().Should().ContainSingle().Which;
        provider.FilePath.Should().Be(Path.Combine(_directory, "test-service.log"));

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Check");
        logger.LogInformation("below min level");
        logger.LogWarning("at min level");

        host.Dispose();

        var text = File.ReadAllText(provider.FilePath);
        text.Should().NotContain("below min level");
        text.Should().Contain("at min level");
    }

    [Fact]
    public void Development_Disabled_DoesNotRegisterFileLogger()
    {
        using var host = Build("Development", ("FileLogging:Directory", _directory), ("FileLogging:Enabled", "false"));

        host.Services.GetServices<ILoggerProvider>().OfType<FileLoggerProvider>().Should().BeEmpty();
    }

    private static IHost Build(string environment, params (string Key, string Value)[] values)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            DisableDefaults = true
        });

        builder.Configuration.AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value));
        ServiceDefaultsExtensions.AddFileLogging(builder, "test-service");

        return builder.Build();
    }
}
