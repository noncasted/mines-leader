namespace Management.Configs;

[GenerateSerializer]
public class ConfigStorageState
{
    [Id(0)]
    public object? Value { get; set; }
}