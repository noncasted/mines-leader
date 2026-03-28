namespace deploy;

public static class Options
{
    public const string HttpsPath = "/https";
    public const string PfxPath = "/https/aspnetapp.pfx";

    public static readonly string PfxPassword = Environment.GetEnvironmentVariable("PFX_PASSWORD") ??
                                                throw new InvalidOperationException(
                                                    "PFX_PASSWORD environment variable is required"
                                                );

    public static readonly Dictionary<int, int> Ports = new()
    {
        { 7000, 7100 }, // Aspire
        { 7001, 7101 }, // Meta
        { 7002, 7102 }, // Game
        { 7003, 7103 }, // Console
    };

    public static readonly Dictionary<string, int> Domains = new()
    {
        { "aspire.minesleader.xyz",   7100 }, // Aspire
        { "gateway.minesleader.xyz",  7101 }, // Meta
        { "server.minesleader.xyz",   7102 }, // Game
        { "console.minesleader.xyz",  7103 }, // Console
    };

    // Private
    // 6001 - Coordinator
    // 6002 - Silo
}