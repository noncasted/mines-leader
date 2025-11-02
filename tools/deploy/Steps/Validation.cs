namespace deploy;

public static class Validation
{
    public static Task Run()
    {
        Console.WriteLine("[Deploy] Validating environment variables...");

        _ = Environment.GetEnvironmentVariable("ASPIRE_TOKEN") ??
            throw new InvalidOperationException("ASPIRE_TOKEN environment variable is required");
        _ = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ??
            throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is required");
        _ = Environment.GetEnvironmentVariable("GAME_SERVER_URL") ??
            throw new InvalidOperationException("GAME_SERVER_URL environment variable is required");
        _ = Environment.GetEnvironmentVariable("PFX_PASSWORD") ??
            throw new InvalidOperationException("PFX_PASSWORD environment variable is required");

        Console.WriteLine("[Deploy] All required environment variables are present");
        return Task.CompletedTask;
    }
}
