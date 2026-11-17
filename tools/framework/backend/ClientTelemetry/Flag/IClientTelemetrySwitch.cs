using Internal;

namespace Game.ClientTelemetry;

// The switch of the whole tracking.
public interface IClientTelemetrySwitch
{
    // The value of the row is known. Off before that says nothing about the row.
    bool IsLoaded { get; }

    // From the memory of the host, no query.
    bool IsEnabled { get; }

    // The listener runs at once, when the value is loaded and on every change.
    void View(IReadOnlyLifetime lifetime, Action listener);

    Task Set(bool enabled);
}
