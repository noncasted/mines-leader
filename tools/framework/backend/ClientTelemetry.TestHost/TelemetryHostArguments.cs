namespace Framework.ClientTelemetry.TestHost;

public sealed class TelemetryHostArguments
{
    public string Token { get; private init; } = "";
    public int Port { get; private init; }
    public string? AddressFile { get; private init; }
    public bool Seed { get; private init; }

    // A file of ProfilerTraceData from the editor: the seed sends it as the startup trace of its sessions.
    public string? StartupFile { get; private init; }

    // The folder with theme.css and app.css of a console: its pages run on this host.
    public string? ConsoleStyles { get; private init; }

    // The console of a game without the telemetry: no reader, no switch, no socket.
    public bool NoTelemetry { get; private init; }

    // ClientTelemetryStorageOptions.LostAfter of this host: a session open on a quiet socket closes as lost soon.
    public TimeSpan LostAfter { get; private init; }

    // --token <value> [--port <n, 0 is any free one>] [--address-file <path>] [--seed] [--startup-file <path>, with --seed] [--console <folder>] [--no-telemetry]
    // [--lost-after <seconds>, 15 by default]
    public static TelemetryHostArguments Parse(string[] args)
    {
        var token = "";
        var port = 0;
        string? addressFile = null;
        var seed = false;
        string? startupFile = null;
        string? consoleStyles = null;
        var noTelemetry = false;
        var lostAfter = TimeSpan.FromSeconds(15);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--token":
                    token = args[++i];
                    break;
                case "--port":
                    port = int.Parse(args[++i]);
                    break;
                case "--address-file":
                    addressFile = args[++i];
                    break;
                case "--seed":
                    seed = true;
                    break;
                case "--startup-file":
                    startupFile = Path.GetFullPath(args[++i]);
                    break;
                case "--console":
                    consoleStyles = Path.GetFullPath(args[++i]);
                    break;
                case "--no-telemetry":
                    noTelemetry = true;
                    break;
                case "--lost-after":
                    lostAfter = TimeSpan.FromSeconds(int.Parse(args[++i]));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument {args[i]}");
            }
        }

        if (token.Length == 0)
            throw new ArgumentException("--token is required: the reading is under it");

        if (noTelemetry && (seed || consoleStyles == null))
            throw new ArgumentException("--no-telemetry is a look at the console: with --console and without --seed");

        if (startupFile != null && !seed)
            throw new ArgumentException("--startup-file goes with --seed: the seed sends it");

        return new TelemetryHostArguments
        {
            Token = token, Port = port, AddressFile = addressFile, Seed = seed, StartupFile = startupFile, ConsoleStyles = consoleStyles, NoTelemetry = noTelemetry,
            LostAfter = lostAfter
        };
    }
}
