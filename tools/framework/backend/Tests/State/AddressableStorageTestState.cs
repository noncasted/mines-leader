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

public class AddressableStorageTestState(AddressableStateUtils utils)
    : AddressableState<AddressableStorageTestOptions>(utils);
