namespace Infrastructure;

public class AddressableStateChannelId<T> : IRuntimeChannelId
{
    public required string Name { get; init; }

    public string ToRaw()
    {
        return $"addressable-state-{Name}";
    }
}
