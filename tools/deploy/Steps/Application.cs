namespace deploy;

public static class Application
{
    public static async Task Run()
    {
        Console.WriteLine("[Deploy] Starting application...");

        var aspireProjectPath = "backend/Orchestration/Aspire";

        Console.WriteLine($"[Deploy] Cleaning project at {aspireProjectPath}...");
        await Command.Run("dotnet", "clean --configuration Release", aspireProjectPath);

        Console.WriteLine("[Deploy] Starting Aspire...");

        var aspire = "aspire run --configuration Release > /var/log/aspire.log 2>&1";
        var runCommand = $"-c \"cd {aspireProjectPath} && {aspire}\"";

        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_Kestrel__Certificates__Default__Path"] = Options.PfxPath,
            ["ASPNETCORE_Kestrel__Certificates__Default__Password"] = Options.PfxPassword,
            ["SSL_CERT_DIR"] = "/etc/ssl/certs",
        };

        var dbConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

        if (dbConnectionString != null)
            environment["DB_CONNECTION_STRING"] = dbConnectionString;

        Command.RunInBackground("bash", runCommand, environment: environment);
    }
}