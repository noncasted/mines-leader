namespace Console;

public interface IAuditLogStorage
{
    Task Write(string action, string details);
    Task<AuditLogResult> GetPage(int offset, int limit);
}
