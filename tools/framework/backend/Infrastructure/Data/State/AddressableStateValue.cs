using Common.Extensions;
using Infrastructure.State;

namespace Infrastructure;

[GenerateSerializer]
public class AddressableStateValue : IDirectStateValue
{
    [Id(0)]
    public string Value { get; init; } = string.Empty;

    [Id(1)]
    public bool IsInitialized { get; init; }

    [Id(2)]
    public DateTime UpdateDate { get; init; }

    public int Version => 0;
}
