namespace Console;

public class AuditLogResult
{
    public required IReadOnlyList<AuditEntry> Entries { get; init; }
    public required int TotalCount { get; init; }
}
