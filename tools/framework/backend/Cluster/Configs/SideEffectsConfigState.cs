using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class SideEffectsConfigState(AddressableStateUtils utils)
    : AddressableState<SideEffectsOptions>(utils), ISideEffectsConfig;
