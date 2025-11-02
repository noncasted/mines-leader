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

        var pfxExport = $"ASPNETCORE_Kestrel__Certificates__Default__Password={Options.PfxPassword}";
        var aspire = "aspire run > /var/log/aspire.log 2>&1";
        
        var runCommand = $"-c \"cd {aspireProjectPath} && {pfxExport} {aspire}\"";
        
        Command.RunInBackground("bash", runCommand);
    }
}