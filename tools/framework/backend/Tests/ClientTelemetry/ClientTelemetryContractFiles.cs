namespace Framework.Tests.ClientTelemetry;

// The files shared with the tests of the Unity client.
public static class ClientTelemetryContractFiles
{
    public static byte[] ReadBytes(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "ClientTelemetry", "Golden", name));
    }
}
