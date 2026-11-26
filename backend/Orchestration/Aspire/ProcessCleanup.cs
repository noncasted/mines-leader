using System.Diagnostics;

namespace Aspire;

// Kills the processes a previous AppHost of this checkout left behind. Other games on the framework run processes
// with the same names (Aspire.AppHost, Silo, MetaGateway, ...) at the same time, so only the executables built in
// the Orchestration folder of this checkout are targets.
public static class ProcessCleanup
{
    public static void Run()
    {
        var currentPid = Environment.ProcessId;
        var currentName = Process.GetCurrentProcess().ProcessName;
        var orchestrationRoot = OrchestrationRoot();

        var targets = Process.GetProcessesByName(currentName)
                             .Where(p => p.Id != currentPid)
                             .ToList();

        targets.AddRange(Process.GetProcessesByName("Silo"));
        targets.AddRange(Process.GetProcessesByName("MetaGateway"));
        targets.AddRange(Process.GetProcessesByName("GameGateway"));
        targets.AddRange(Process.GetProcessesByName("ConsoleGateway"));
        targets.AddRange(Process.GetProcessesByName("Coordinator"));

        var own = targets.Where(p => p.MainModule!.FileName.StartsWith(orchestrationRoot, StringComparison.Ordinal))
                         .ToList();

        Parallel.ForEach(own, process => {
            try
            {
                Console.WriteLine($"[ProcessCleanup] Killing old process: {process.ProcessName} (pid={process.Id})");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessCleanup] Failed to kill pid={process.Id}: {ex.Message}");
            }
        });
    }

    // backend/Orchestration/ of this checkout, with the trailing separator: the AppHost runs from
    // backend/Orchestration/Aspire/bin/<configuration>/<framework>/.
    private static string OrchestrationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory.Name != "Orchestration")
        {
            directory = directory.Parent
                        ?? throw new InvalidOperationException(
                            $"The AppHost does not run from backend/Orchestration: {AppContext.BaseDirectory}");
        }

        return directory.FullName + Path.DirectorySeparatorChar;
    }
}
