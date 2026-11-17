using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class RuntimeChannelConfigState(AddressableStateUtils utils)
    : AddressableState<RuntimeChannelOptions>(utils), IRuntimeChannelConfig;
