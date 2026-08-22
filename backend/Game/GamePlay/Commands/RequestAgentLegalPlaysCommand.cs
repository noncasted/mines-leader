using Cluster.Configs;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public class RequestAgentLegalPlaysCommand : ResponseCommand<SharedAgentLegalPlaysRequest, SharedAgentLegalPlaysResponse>
{
    public RequestAgentLegalPlaysCommand(GameCommandUtils utils, ICardConfigs cardConfigs)
    {
        _utils = utils;
        _cardConfigs = cardConfigs;
    }

    private readonly GameCommandUtils _utils;
    private readonly ICardConfigs _cardConfigs;

    protected override SharedAgentLegalPlaysResponse Execute(IUser user, SharedAgentLegalPlaysRequest request)
    {
        var currentPlayerId = _utils.GameRound.CurrentPlayer.Value?.User.Id;
        return AgentLegalPlaysBuilder.Build(
            _utils.GameContext,
            user.Id,
            currentPlayerId,
            _cardConfigs.Value);
    }
}
