namespace Infrastructure.State;

[GenerateSerializer]
public class EventPayload
{
    [Id(0)]
    public required string Type { get; init; }

    [Id(1)]
    public required string Json { get; init; }
}
