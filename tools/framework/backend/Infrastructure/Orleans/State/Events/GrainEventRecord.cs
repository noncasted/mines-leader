namespace Infrastructure.State;

[GenerateSerializer]
public class GrainEventRecord
{
    [Id(0)]
    public required string StreamId { get; init; }

    [Id(1)]
    public required List<EventPayload> Events { get; init; }
}
