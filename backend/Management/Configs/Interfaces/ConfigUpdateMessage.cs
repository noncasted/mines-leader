using Infrastructure.Messaging;

namespace Management.Configs;

[GenerateSerializer]
public class ConfigUpdateMessage : IClusterMessage
{
    [Id(0)]
    public required object Value { get; init; }
}