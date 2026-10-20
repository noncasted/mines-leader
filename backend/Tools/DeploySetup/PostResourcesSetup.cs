using Microsoft.Extensions.Configuration;

namespace DeploySetup;

public static class PostResourcesSetup
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public static Task Run(IConfigurationManager configuration)
    {
        return RunWithRetries(attempt => RunAttempt(configuration, attempt), MaxAttempts, RetryDelay);
    }

    // Throws after the last failed attempt: the migrator must exit non-zero, otherwise compose
    // (service_completed_successfully) starts the silo on a broken schema.
    public static async Task RunWithRetries(Func<int, Task> setup, int maxAttempts, TimeSpan retryDelay)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "At least one attempt is required");

        var failures = new List<Exception>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await setup(attempt);
                return;
            }
            catch (Exception ex)
            {
                failures.Add(ex);
                Console.WriteLine($"[DeploySetup] Setup attempt {attempt}/{maxAttempts} failed: {ex.Message}");

                if (attempt < maxAttempts)
                    await Task.Delay(retryDelay);
            }
        }

        throw new AggregateException($"[DeploySetup] Setup failed after {maxAttempts} attempts", failures);
    }

    private static async Task RunAttempt(IConfigurationManager configuration, int attempt)
    {
        var localSection = configuration.GetSection("Local");

        var requiresDrop = localSection.GetSection("DropStates").Get<bool>() ||
                           Environment.GetEnvironmentVariable("DropStates") == "TRUE";

        var requiresCleanup = localSection.GetSection("ClearStates").Get<bool>() ||
                              Environment.GetEnvironmentVariable("ClearStates") == "TRUE";

        Console.WriteLine(
            $"[DeploySetup] Setup attempt {attempt}/{MaxAttempts} (DropStates={requiresDrop}, ClearStates={requiresCleanup})");

        if (requiresDrop)
            await StatesDrop.Run(configuration);

        await OrleansClusteringSetup.Run(configuration);
        await StatesSetup.Run(configuration);
        await SideEffectsSetup.Run(configuration);
        await BenchmarkSetup.Run(configuration);
        await AuditLogSetup.Run(configuration);

        if (requiresCleanup)
            await StatesCleanup.Run(configuration);

        Console.WriteLine("[DeploySetup] Post-setup completed successfully");
    }
}
