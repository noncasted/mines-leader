using Shared;

namespace Game.GamePlay;

public class RequestAgentObservationCommand(GameCommandUtils utils)
    : GameCommand<SharedAgentObservationRequest>(utils)
{
    protected override bool RequestOracle => true;

    protected override EmptyResponse Execute(Context context, SharedAgentObservationRequest request)
    {
        return EmptyResponse.Ok;
    }
}
