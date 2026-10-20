using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Tests.Telemetry;

/// <summary>
/// F2.39: dev-only file telemetry (metrics snapshots, session logs) fails without a repository root
/// instead of silently writing nothing.
/// F2.40: a git worktree (.git is a "gitdir:" file) is a repository root too.
/// </summary>
public class TelemetryPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"telemetry-paths-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void RequireTelemetryDir_NoRepositoryRoot_Throws()
    {
        var start = Path.Combine(_root, "bin", "Debug");
        Directory.CreateDirectory(start);

        var act = () => TelemetryPaths.RequireTelemetryDir("metrics", start);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no repository root*");
    }

    [Fact]
    public void RequireTelemetryDir_UnderRepository_CreatesDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var start = Path.Combine(_root, "backend", "bin", "Debug");
        Directory.CreateDirectory(start);

        var dir = TelemetryPaths.RequireTelemetryDir("metrics", start);

        dir.Should().Be(Path.Combine(_root, "backend", ".telemetry", "metrics"));
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void RequireTelemetryDir_UnderWorktree_UsesWorktreeRoot()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: /repo/.git/worktrees/wt\n");
        var start = Path.Combine(_root, "backend", "bin", "Debug");
        Directory.CreateDirectory(start);

        var dir = TelemetryPaths.RequireTelemetryDir("logs", start);

        dir.Should().Be(Path.Combine(_root, "backend", ".telemetry", "logs"));
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void FindProjectRoot_GitFileWithoutGitdir_Throws()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, ".git"), "not a worktree link");
        var start = Path.Combine(_root, "backend", "bin");
        Directory.CreateDirectory(start);

        var act = () => TelemetryPaths.FindProjectRoot(start);

        act.Should().Throw<InvalidOperationException>().WithMessage("*gitdir*");
    }

    [Fact]
    public async Task MetricsSnapshot_DevelopmentWithoutRoot_FailsToStart()
    {
        var service = new MetricsSnapshotService(NullLogger<MetricsSnapshotService>.Instance,
            Environment(Environments.Development),
            () => throw new InvalidOperationException("no repository root"));

        var act = () => service.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        service.Dispose();
    }

    [Fact]
    public async Task MetricsSnapshot_Production_DoesNotResolveDirectory()
    {
        var resolved = false;

        var service = new MetricsSnapshotService(NullLogger<MetricsSnapshotService>.Instance,
            Environment(Environments.Production),
            () => {
                resolved = true;
                return _root;
            });

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        service.Dispose();

        resolved.Should().BeFalse("file snapshots are dev only");
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
