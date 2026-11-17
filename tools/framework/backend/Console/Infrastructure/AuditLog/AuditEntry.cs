namespace Console;

public class AuditEntry
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Action { get; init; }
    public required string Details { get; init; }
}
