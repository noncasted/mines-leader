using System.Diagnostics;
using DeploySetup;
using FluentAssertions;
using Xunit;

namespace Tests.Deploy;

/// <summary>
/// F2.1: the migrator must fail (exception, non-zero exit code) when every setup attempt fails,
/// otherwise compose (service_completed_successfully) starts the silo on a broken schema.
/// </summary>
public class PostResourcesSetupTests
{
    [Fact]
    public async Task AllAttemptsFail_Throws()
    {
        var attempts = 0;

        var run = () => PostResourcesSetup.RunWithRetries(attempt => {
            attempts++;
            throw new InvalidOperationException($"attempt {attempt} failed");
        }, maxAttempts: 3, retryDelay: TimeSpan.Zero);

        var error = await run.Should().ThrowAsync<AggregateException>();

        attempts.Should().Be(3);
        error.Which.InnerExceptions.Select(e => e.Message)
             .Should().Equal("attempt 1 failed", "attempt 2 failed", "attempt 3 failed");
    }

    [Fact]
    public async Task SucceedsAfterFailures_DoesNotThrow()
    {
        var attempts = 0;

        await PostResourcesSetup.RunWithRetries(_ => {
            attempts++;

            if (attempts < 3)
                throw new InvalidOperationException("not yet");

            return Task.CompletedTask;
        }, maxAttempts: 5, retryDelay: TimeSpan.Zero);

        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Migrator_UnreachableDatabase_ExitsWithNonZeroCode()
    {
        var output = AppContext.BaseDirectory;
        var migrator = Path.Combine(output, "DeploySetup.dll");

        File.Exists(migrator).Should().BeTrue("the test project references DeploySetup");

        // Port 1 refuses at once: every attempt fails fast, the run takes the 4 retry delays (~20 s).
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList =
            {
                "exec",
                "--runtimeconfig", Path.Combine(output, "Tests.runtimeconfig.json"),
                "--depsfile", Path.Combine(output, "Tests.deps.json"),
                migrator,
                "--connection=Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2"
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        start.Environment.Remove("DropStates");
        start.Environment.Remove("ClearStates");

        using var process = Process.Start(start)!;

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await process.WaitForExitAsync(timeout.Token);

        var log = await stdout + await stderr;

        process.ExitCode.Should().NotBe(0, log);
        log.Should().Contain("Setup failed after 5 attempts");
        log.Should().NotContain("[DeploySetup] Done");
    }
}
