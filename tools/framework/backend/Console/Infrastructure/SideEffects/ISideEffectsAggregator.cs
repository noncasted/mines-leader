using Internal;

namespace Console;

public interface ISideEffectsAggregator
{
    ISharedViewableProperty<SideEffectsAggregate> Aggregate { get; }
}
