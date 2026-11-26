using FluentAssertions;
using Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Tests.Telemetry;

public class FileLoggerProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"file-logger-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    // The games read the options with ConfigurationBinder: it silently skips a private setter, so these keys prove the
    // properties stay bindable.
    [Fact]
    public void Options_AreReadFromTheConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileLogging:Enabled"] = "false",
                ["FileLogging:Directory"] = "/logs/custom",
                ["FileLogging:MinLevel"] = "Warning"
            })
            .Build();

        var options = configuration.GetSection(FileLoggerOptions.SectionName).Get<FileLoggerOptions>()!;

        options.Enabled.Should().BeFalse();
        options.Directory.Should().Be("/logs/custom");
        options.MinLevel.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Options_KeepDefaults_ForMissingKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileLogging:Directory"] = "/logs/custom" })
            .Build();

        var options = configuration.GetSection(FileLoggerOptions.SectionName).Get<FileLoggerOptions>()!;

        options.Enabled.Should().BeTrue();
        options.MinLevel.Should().Be(LogLevel.Trace);
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
}
