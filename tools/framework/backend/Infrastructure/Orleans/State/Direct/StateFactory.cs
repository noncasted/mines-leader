using System.Reflection;

namespace Infrastructure.State;

public interface IStateFactory
{
    State<TState> Create<TState>(IGrainContext context) where TState : class, IDirectStateValue, new();
}

public class StateFactory : IStateFactory
{
    public StateFactory(IStateStorage stateStorage)
    {
        _stateStorage = stateStorage;
    }

    private readonly IStateStorage _stateStorage;

    public State<TState> Create<TState>(IGrainContext context) where TState : class, IDirectStateValue, new()
    {
        return new State<TState>(_stateStorage, context);
    }
}
