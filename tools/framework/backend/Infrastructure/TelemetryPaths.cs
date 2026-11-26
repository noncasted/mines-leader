namespace Infrastructure;

public static class TelemetryPaths
{
    public static string? FindProjectRoot()
    {
        return FindProjectRoot(AppContext.BaseDirectory);
    }

    public static string? FindProjectRoot(string startDirectory)
    {
        var dir = startDirectory;

        for (var i = 0; i < 10; i++)
        {
            dir = Path.GetDirectoryName(dir);

            if (dir == null)
                return null;

            if (IsRepositoryRoot(dir))
                return dir;
        }

        return null;
    }

    // .git is a directory in a regular clone and a file ("gitdir: <path>") in a git worktree or submodule.
    private static bool IsRepositoryRoot(string dir)
    {
        var git = Path.Combine(dir, ".git");

        if (Directory.Exists(git))
            return true;

        if (File.Exists(git) == false)
            return false;

        using var reader = new StreamReader(git);
        var firstLine = reader.ReadLine();

        if (firstLine == null || firstLine.StartsWith("gitdir:", StringComparison.Ordinal) == false)
            throw new InvalidOperationException($"File telemetry: {git} is a file, but not a git worktree link (gitdir:)");

        return true;
    }

    // Null when there is no repository root: for callers with an explicit fallback directory.
    public static string? GetTelemetryDir(string subfolder)
    {
        var root = FindProjectRoot();

        if (root == null)
            return null;

        return CreateTelemetryDir(root, subfolder);
    }

    // For writers that exist only for local runs from the repository: no repository root is a setup error.
    public static string RequireTelemetryDir(string subfolder)
    {
        return RequireTelemetryDir(subfolder, AppContext.BaseDirectory);
    }

    public static string RequireTelemetryDir(string subfolder, string startDirectory)
    {
        var root = FindProjectRoot(startDirectory) ?? throw new InvalidOperationException(
            $"File telemetry '{subfolder}': no repository root (.git) above {startDirectory}");

        return CreateTelemetryDir(root, subfolder);
    }

    private static string CreateTelemetryDir(string root, string subfolder)
    {
        var dir = Path.Combine(root, "backend", ".telemetry", subfolder);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
