namespace Management.Configs;

[GenerateSerializer]
public class ConfigUpdateMessage
{
    [Id(0)]
    public required object Value { get; init; }
}