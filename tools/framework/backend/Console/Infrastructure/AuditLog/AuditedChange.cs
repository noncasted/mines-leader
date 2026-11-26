using BlazorBlueprint.Components;

namespace Console;

// The audit row of a change that is already done: a failed write says so and does not report the change itself as failed.
public static class AuditedChange
{
    public static async Task Report(this IAuditLogStorage log, ToastService toasts, string action, string details, string saved)
    {
        try
        {
            await log.Write(action, details);
        }
        catch (Exception exception)
        {
            toasts.Warning($"{saved}, audit failed: {exception.Message}", "Saved");
            return;
        }

        toasts.Success(saved, "Saved");
    }
}
