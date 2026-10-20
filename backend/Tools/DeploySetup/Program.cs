using DeploySetup;
using Microsoft.Extensions.Configuration;

Console.WriteLine("[DeploySetup] Starting idempotent database migrations");

var configuration = new ConfigurationManager();

configuration.AddEnvironmentVariables();

var connectionFromCli = args.FirstOrDefault(a => a.StartsWith("--connection="))?.Substring("--connection=".Length);

if (connectionFromCli != null)
{
    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:postgres"] = connectionFromCli,
        ["postgres"] = connectionFromCli,
    });
}

try
{
    await PostResourcesSetup.Run(configuration);
}
catch (Exception e)
{
    // A failed migration must stop the deploy: compose starts the silo only after a zero exit code.
    Console.Error.WriteLine($"[DeploySetup] Failed: {e}");
    return 1;
}

Console.WriteLine("[DeploySetup] Done");
return 0;
