namespace Infrastructure.State;

public static class EventStateExtensions
{
    public static async Task<T> Apply<T>(this EventState<T> state, params object[] events)
        where T : class, IEventStateValue, new()
    {
        await state.Load();
        await state.Append(events);
        await state.Write();
        return state.Value;
    }
}