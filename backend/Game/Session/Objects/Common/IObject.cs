using Common.Reactive;

namespace Game.Session;

public interface IObject
{
    int Id { get; }
    IReadOnlyLifetime Lifetime { get; }

    IReadOnlyDictionary<int, IObjectProperty> Properties { get; }
}