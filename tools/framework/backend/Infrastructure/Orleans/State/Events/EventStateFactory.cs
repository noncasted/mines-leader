using System.Reflection;

namespace Infrastructure.State;

public interface IEventStateFactory
{
    EventState<TAggregate> Create<TAggregate>(IGrainContext context)
        where TAggregate : class, IEventStateValue, new();
}

public class EventStateFactory : IEventStateFactory
{
    public EventStateFactory(
        IEventStorage eventStorage,
        IGrainStatesRegistry statesRegistry,
        IStateSerializer stateSerializer)
    {
        _eventStorage = eventStorage;
        _statesRegistry = statesRegistry;
        _stateSerializer = stateSerializer;
    }

    private readonly IEventStorage _eventStorage;
    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IStateSerializer _stateSerializer;

    public EventState<TAggregate> Create<TAggregate>(IGrainContext context)
        where TAggregate : class, IEventStateValue, new()
    {
        return new EventState<TAggregate>(_eventStorage, context, _statesRegistry, _stateSerializer);
    }
}
