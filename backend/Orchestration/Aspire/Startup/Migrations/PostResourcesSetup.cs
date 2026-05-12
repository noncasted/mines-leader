using Microsoft.Extensions.Configuration;

namespace Aspire.Startup;

public static class PostResourcesSetup
{
    private const int MaxAttempts = 5;

    public static async Task Run(IConfigurationManager configuration)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var localSection = configuration.GetSection("Local");

                var requiresDrop = localSection.GetSection("DropStates").Get<bool>() ||
                                   Environment.GetEnvironmentVariable("DropStates") == "TRUE";

                var requiresCleanup = localSection.GetSection("ClearStates").Get<bool>() ||
                                      Environment.GetEnvironmentVariable("ClearStates") == "TRUE";

                Console.WriteLine(
                    $"[Migrations] Setup attempt {attempt}/{MaxAttempts} (DropStates={requiresDrop}, ClearStates={requiresCleanup})");

                if (requiresDrop)
                    await StatesDrop.Run(configuration);

                await OrleansClusteringSetup.Run(configuration);
                await StatesSetup.Run(configuration);
                await SideEffectsSetup.Run(configuration);
                await BenchmarkSetup.Run(configuration);
                await AuditLogSetup.Run(configuration);

                if (requiresCleanup)
                    await StatesCleanup.Run(configuration);

                Console.WriteLine("[Migrations] Post-setup completed successfully");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Migrations] Setup attempt {attempt}/{MaxAttempts} failed: {ex.Message}");

                if (attempt < MaxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        Console.WriteLine($"[Migrations] Setup failed after {MaxAttempts} attempts");
    }
}
