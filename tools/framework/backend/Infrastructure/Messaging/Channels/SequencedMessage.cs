namespace Infrastructure;

[GenerateSerializer]
public class SequencedMessage
{
    [Id(0)] public long Sequence { get; init; }
    [Id(1)] public object Payload { get; init; } = null!;

    // The activation of the channel that numbered the message: a new activation numbers from 1 again.
    [Id(2)] public Guid Epoch { get; init; }
}
