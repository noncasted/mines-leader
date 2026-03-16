using Common.Extensions;
using Common.Reactive;

namespace Infrastructure;

public interface IRawAddressableState<T> : IAddressableState<T> where T : class, new()
{
    bool IsInitialized { get; }
}

[GenerateSerializer]
public class RawAddressableState
{
    [Id(0)]
    public string Raw { get; set; }

    [Id(1)]
    public bool IsInitialized { get; set; }
}

public abstract class RawAddressableStateView<TView, TState> :
    AddressableState<TState>,
    IRawAddressableState<TView>
    where TView : class, new()
    where TState : RawAddressableState, new()
{
    public RawAddressableStateView(IOrleans orleans, IMessaging messaging) : base(orleans, messaging)
    {
        Value = new TView();
    }

    public new TView Value { get; private set; }
    public bool IsInitialized { get; private set; }

    protected override void OnSetup(IReadOnlyLifetime lifetime)
    {
        this!.ViewNotNull<TState>(lifetime, state =>
            {
                try
                {
                    var newValue = JsonUtils.Deserialize<TView>(state.Raw)!;
                    Value = newValue;
                    IsInitialized = state.IsInitialized;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        );
    }

    public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TView> handler)
    {
        Advise(lifetime, (valueLifetime, raw) =>
            {
                var value = JsonUtils.Deserialize<TView>(raw.Raw)!;
                handler(valueLifetime, value);
            }
        );
    }

    public Task SetValue(TView value)
    {
        return SetValue(new TState()
            {
                Raw = JsonUtils.Serialize(value),
                IsInitialized = true
            }
        );
    }
}