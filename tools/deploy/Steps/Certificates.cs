namespace deploy;

public static class Certificates
{
    public static async Task Run()
    {
        Console.WriteLine("[Deploy] Setting up .NET dev certificates...");

        await Command.Run("dotnet", "dev-certs https --clean");

        Directory.CreateDirectory(Options.HttpsPath);

        await Command.Run("chmod", "755 " + Options.HttpsPath);
        await Command.Run("dotnet", $"dev-certs https -ep {Options.PfxPath} -p {Options.PfxPassword}");

        Console.WriteLine("[Deploy] Certificates setup completed");
    }
}