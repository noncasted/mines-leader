using Common;

namespace Game;

public interface IObject
{
    int Id { get; }
    IReadOnlyLifetime Lifetime { get; }

    IReadOnlyDictionary<int, IObjectProperty> Properties { get; }
}