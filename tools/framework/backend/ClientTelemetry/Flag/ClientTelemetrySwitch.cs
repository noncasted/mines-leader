using Infrastructure;
using Internal;

namespace Game.ClientTelemetry;

// AddressableState keeps the value, the channel to the other hosts and the reload after a gap of the channel. Its IsInitialized does
// not tell "loaded": it stays false for a row that was never written, and off is the default of that row.
public class ClientTelemetrySwitch : IClientTelemetrySwitch, IOrleansStarted
{
    public ClientTelemetrySwitch(AddressableStateUtils utils)
    {
        _state = new AddressableState<ClientTelemetrySettingsState>(utils);
    }

    private readonly AddressableState<ClientTelemetrySettingsState> _state;

    // The pages of the console view the switch from their circuits while the channel applies the row: the row has one
    // subscriber, this switch, and the pages subscribe to these shared values.
    private readonly SharedViewableProperty<bool> _isLoaded = new(false);
    private readonly SharedViewableProperty<bool> _isEnabled = new(false);

    public bool IsLoaded => _isLoaded.Value;
    public bool IsEnabled => _isEnabled.Value;

    // A failed read stops the start of the host, like any AddressableState.
    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        // Before the read: the row is applied only from inside it and from the channel it starts listening to.
        _state.Advise(lifetime, (_, state) => _isEnabled.Set(state.IsEnabled));

        await _state.OnOrleansStarted(lifetime);
        _isLoaded.Set(true);
    }

    public void View(IReadOnlyLifetime lifetime, Action listener)
    {
        // Subscribed first: a change between the subscriptions and the first call is not lost, at worst it is told twice.
        _isLoaded.Advise(lifetime, (_, _) => listener());
        _isEnabled.Advise(lifetime, (_, _) => listener());
        listener();
    }

    public Task Set(bool enabled)
    {
        if (!IsLoaded)
            throw new InvalidOperationException("The client telemetry switch is not loaded yet");

        return _state.SetValue(new ClientTelemetrySettingsState { IsEnabled = enabled });
    }
}
