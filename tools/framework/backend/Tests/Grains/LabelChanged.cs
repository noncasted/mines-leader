using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public class LabelChanged
{
    public string Label { get; init; } = string.Empty;
}
