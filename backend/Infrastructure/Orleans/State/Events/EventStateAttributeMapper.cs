using System.Reflection;
using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.State;

public class EventStateAttributeMapper : IAttributeToFactoryMapper<EventStateAttribute>
{
    private readonly MethodInfo _createMethodInfo = typeof(IEventStateFactory).GetMethod("Create").ThrowIfNull();

    public Factory<IGrainContext, object> GetFactory(ParameterInfo parameter, EventStateAttribute attribute)
    {
        var parameterType = parameter.ParameterType;

        if (!parameterType.IsGenericType || typeof(EventState<>) != parameterType.GetGenericTypeDefinition())
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' on the constructor for '{parameter.Member.DeclaringType}' has an unsupported type, '{parameterType}'. " +
                $"It must be an instance of generic type '{typeof(EventState<>)}' because it has an associated [EventState] attribute.",
                parameter.Name);
        }

        var genericCreate = _createMethodInfo.MakeGenericMethod(parameterType.GetGenericArguments());
        return context => Create(context, genericCreate);
    }

    private static object Create(IGrainContext context, MethodInfo genericCreate)
    {
        var factory = context.ActivationServices.GetRequiredService<IEventStateFactory>();
        object[] args = [context];
        return genericCreate.Invoke(factory, args).ThrowIfNull();
    }
}

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
