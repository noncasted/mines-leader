using Infrastructure;
using Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Console.Monitoring;

public class HeapReportStorage : IHeapReportStorage
{
    public HeapReportStorage(ILogger<HeapReportStorage> logger)
    {
        _logger = logger;
        _directory = ResolveDirectory();
        System.IO.Directory.CreateDirectory(_directory);
        _logger.LogInformation("[HeapReport] Storage directory: {Directory}", _directory);
    }

    private const string FilePrefix = "heap-report-";
    private const string FileSuffix = ".txt";

    private readonly string _directory;
    private readonly ILogger<HeapReportStorage> _logger;
    private readonly object _lock = new();

    public string Directory => _directory;

    public IReadOnlyList<HeapReport> List()
    {
        lock (_lock)
        {
            if (!System.IO.Directory.Exists(_directory))
                return [];

            return System.IO.Directory
                         .EnumerateFiles(_directory, $"{FilePrefix}*{FileSuffix}")
                         .Select(path =>
                         {
                             var info = new FileInfo(path);
                             return new HeapReport
                             {
                                 FileName = info.Name,
                                 Timestamp = info.LastWriteTimeUtc,
                                 SizeBytes = info.Length,
                             };
                         })
                         .OrderByDescending(r => r.Timestamp)
                         .ToList();
        }
    }

    public HeapReport Save(IReadOnlyList<HeapSnapshotResponse> snapshots, DateTime timestamp, bool deep)
    {
        var mode = deep ? "deep" : "quick";
        var fileName = $"{FilePrefix}{mode}-{timestamp:yyyyMMdd-HHmmss}{FileSuffix}";
        var filePath = Path.Combine(_directory, fileName);
        var text = HeapReportFormatter.FormatReport(timestamp, snapshots);

        lock (_lock)
        {
            File.WriteAllText(filePath, text);
        }

        var info = new FileInfo(filePath);
        _logger.LogInformation("[HeapReport] Saved report {FileName} ({Size} bytes)", fileName, info.Length);

        return new HeapReport
        {
            FileName = info.Name,
            Timestamp = info.LastWriteTimeUtc,
            SizeBytes = info.Length,
        };
    }

    public byte[] ReadBytes(string fileName)
    {
        var filePath = ResolveSafePath(fileName);

        lock (_lock)
        {
            return File.ReadAllBytes(filePath);
        }
    }

    private string ResolveSafePath(string fileName)
    {
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            throw new ArgumentException($"Invalid file name: {fileName}");

        if (!fileName.StartsWith(FilePrefix) || !fileName.EndsWith(FileSuffix))
            throw new ArgumentException($"Invalid file name: {fileName}");

        return Path.Combine(_directory, fileName);
    }

    private static string ResolveDirectory()
    {
        var fromEnv = Environment.GetEnvironmentVariable("HEAP_REPORTS_DIR");

        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var fromTelemetry = TelemetryPaths.GetTelemetryDir("heap-reports");

        if (fromTelemetry != null)
            return fromTelemetry;

        return Path.Combine(AppContext.BaseDirectory, "heap-reports");
    }
}
