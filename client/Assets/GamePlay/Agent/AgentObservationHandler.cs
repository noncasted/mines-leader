using Internal;
using Network;
using Shared;

namespace GamePlay.Agent {
    public class AgentObservationHandler : OneWayCommand<SharedAgentObservation> {
        protected override void Execute(IReadOnlyLifetime lifetime, SharedAgentObservation context) {
            GameAgentBridge.OnObservation(context);
        }
    }
}
