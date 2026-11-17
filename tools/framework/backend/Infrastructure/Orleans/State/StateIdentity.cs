using Common.Extensions;

namespace Infrastructure.State;

// Equal by value: two records of one state of one grain are one row (StateStorageExtensions.ToIdentityMap).
public class StateIdentity : IEquatable<StateIdentity>
{
    public required object Key { get; init; }
    public required string Type { get; init; }
    public required string TableName { get; init; }
    public required string? Extension { get; init; }

    public bool Equals(StateIdentity? other)
    {
        return other is not null
               && Key.Equals(other.Key)
               && Type == other.Type
               && TableName == other.TableName
               && Extension == other.Extension;
    }

    public override bool Equals(object? obj) => Equals(obj as StateIdentity);

    public override int GetHashCode() => HashCode.Combine(Key, Type, TableName, Extension);

    public override string ToString() => Extension == null
        ? $"{TableName}/{Type}/{Key}"
        : $"{TableName}/{Type}/{Key}/{Extension}";
}
