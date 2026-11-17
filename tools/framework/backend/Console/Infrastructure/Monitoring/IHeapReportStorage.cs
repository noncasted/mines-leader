using Infrastructure.Diagnostics;

namespace Console.Monitoring;

public interface IHeapReportStorage
{
    string Directory { get; }
    IReadOnlyList<HeapReport> List();
    HeapReport Save(IReadOnlyList<HeapSnapshotResponse> snapshots, DateTime timestamp, bool deep);
    byte[] ReadBytes(string fileName);
}
