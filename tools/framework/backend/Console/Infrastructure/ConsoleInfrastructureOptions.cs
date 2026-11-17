namespace Console;

public class ConsoleInfrastructureOptions
{
    // The folder of the json configs of the coordinator, when the console runs next to the sources of the game; null: the page of the
    // infrastructure options has no "Write JSON" (a published container has no such folder).
    public string? CoordinatorConfigFolder { get; init; }
}
