using Cluster.Configs;
using Common;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.State;

[GrainState(Table = "configs", State = "addressable_storage_test_config", Lookup = "AddressableStorageTestConfig",
    Key = GrainKeyType.String)]
public class AddressableStorageTestOptions
{
    public string Label { get; init; } = string.Empty;
    public int Limit { get; init; }
}
