using System.Diagnostics;

namespace deploy;

public static class Command
{
    public static async Task<int> Run(
        string command,
        string arguments,
        string? workingDirectory = null,
        bool ignoreErrors = false)
    {
        var processInfo = new ProcessStartInfo(command)
        {
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (string.IsNullOrEmpty(workingDirectory) == false)
            processInfo.WorkingDirectory = workingDirectory;

        var process = Process.Start(processInfo);

        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {command}");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (!ignoreErrors && process.ExitCode != 0)
        {
            var errorMsg = $"Command failed: {command} {arguments}\nExit code: {process.ExitCode}\nError: {error}";
            throw new InvalidOperationException(errorMsg);
        }

        if (!string.IsNullOrEmpty(output))
        {
            Console.WriteLine($"[{command}] {output}");
        }

        return process.ExitCode;
    }

    public static void RunInBackground(
        string command,
        string arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environment = null)
    {
        var processInfo = new ProcessStartInfo(command)
        {
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (string.IsNullOrEmpty(workingDirectory) == false)
            processInfo.WorkingDirectory = workingDirectory;

        if (environment != null)
        {
            foreach (var (key, value) in environment)
                processInfo.Environment[key] = value;
        }

        var process = Process.Start(processInfo);

        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {command}");

        Console.WriteLine($"[Deploy] Started {command} in background (PID: {process.Id})");
    }
}