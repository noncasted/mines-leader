namespace Infrastructure;

// Where a channel stood when an observer was bound: what it publishes after this goes to the observer's queue, so a
// message that does not reach the observer is found by a catch-up from here.
[GenerateSerializer]
public class ChannelPosition
{
    [Id(0)] public long Sequence { get; init; }
    [Id(1)] public Guid Epoch { get; init; }
}
