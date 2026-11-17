using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class RuntimePipeConfigState(AddressableStateUtils utils)
    : AddressableState<RuntimePipeOptions>(utils), IRuntimePipeConfig;
