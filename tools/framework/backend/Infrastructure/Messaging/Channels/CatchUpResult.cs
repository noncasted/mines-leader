namespace Infrastructure;

[GenerateSerializer]
public class CatchUpResult
{
    [Id(0)] public IReadOnlyList<SequencedMessage> Messages { get; init; } = Array.Empty<SequencedMessage>();
    [Id(1)] public bool GapDetected { get; init; }
    [Id(2)] public long CurrentSequence { get; init; }
    [Id(3)] public Guid Epoch { get; init; }
}
