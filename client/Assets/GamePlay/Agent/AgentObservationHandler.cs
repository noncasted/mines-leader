#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Internal;
using Shared;

namespace GamePlay.Agent
{
    public class AgentObservationHandler : OneWayCommand<SharedAgentObservation>
    {
        public AgentObservationHandler(AgentObservations observations)
        {
            _observations = observations;
        }

        private readonly AgentObservations _observations;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedAgentObservation context)
        {
            _observations.Receive(context);
        }
    }
}
#endif
