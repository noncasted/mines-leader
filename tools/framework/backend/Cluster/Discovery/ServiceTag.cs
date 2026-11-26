using Newtonsoft.Json;

namespace Cluster.Discovery;

// Role of a cluster service: an open string, so a module can add its own role without changing the cluster.
// The value is what the console and the logs show and what the persisted service overviews hold.
[GenerateSerializer]
[Immutable]
[JsonConverter(typeof(ServiceTagJsonConverter))]
public readonly struct ServiceTag : IEquatable<ServiceTag>
{
    public ServiceTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Service tag must be a non-empty string", nameof(value));

        _value = value;
    }

    // The coordinator role is a part of the cluster itself (deploy identity, heartbeat, setup). Every other role
    // belongs to the composition root of the project (its hosts and IGameModule), which also sets the required
    // set through ConfigureRequiredServices.
    public static readonly ServiceTag Coordinator = new("Coordinator");

    [Id(0)] private readonly string? _value;

    public string Value => _value ?? throw new InvalidOperationException("Service tag is not initialized");

    public bool Equals(ServiceTag other) => string.Equals(_value, other._value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is ServiceTag other && Equals(other);
    public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    public override string ToString() => Value;

    public static bool operator ==(ServiceTag left, ServiceTag right) => left.Equals(right);
    public static bool operator !=(ServiceTag left, ServiceTag right) => left.Equals(right) == false;
}
