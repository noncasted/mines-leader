namespace deploy;

public static class Certificates
{
    private const string CertCrtPath = "/usr/local/share/ca-certificates/aspnetapp.crt";

    public static async Task Run()
    {
        Console.WriteLine("[Deploy] Setting up .NET dev certificates...");

        await Command.Run("dotnet", "dev-certs https --clean");

        Directory.CreateDirectory(Options.HttpsPath);

        await Command.Run("chmod", "755 " + Options.HttpsPath);
        await Command.Run("dotnet", $"dev-certs https -ep {Options.PfxPath} -p {Options.PfxPassword}");

        Console.WriteLine("[Deploy] Trusting certificate on Linux...");

        await Command.Run("openssl", $"pkcs12 -in {Options.PfxPath} -clcerts -nokeys -out {CertCrtPath} -passin pass:{Options.PfxPassword} -legacy");
        await Command.Run("update-ca-certificates", "");

        Console.WriteLine("[Deploy] Certificates setup completed");
    }
}