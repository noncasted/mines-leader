using Infrastructure.State;

namespace Cluster.Deploy;

// A persistent state keyed by the DeployId (one record per deploy). IDeployCleanup removes the records of every
// deploy except the current one. A module marks its own state with this interface: the cleanup finds the
// marked states in IGrainStatesRegistry, so every host cleans them, also the ones it does not run itself.
// The state must be registered with Key = GrainKeyType.Guid.
public interface IDeployScopedState : IStateValue
{
}
