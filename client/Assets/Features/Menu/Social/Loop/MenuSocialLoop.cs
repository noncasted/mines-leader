using Common.Network;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;

namespace Menu
{
    public class MenuSocialLoop : IMenuSocialLoop
    {
        private readonly INetworkSession _session;
        private readonly IBackendMatchmaking _matchmaking;
        private readonly IMenuPlayerFactory _playerFactory;

        public MenuSocialLoop(
            INetworkSession session,
            IBackendMatchmaking matchmaking,
            IMenuPlayerFactory playerFactory)
        {
            _session = session;
            _matchmaking = matchmaking;
            _playerFactory = playerFactory;
        }

        public async UniTask Start(IReadOnlyLifetime lifetime)
        {
            var lobby = await _matchmaking.SearchLobby(lifetime);
            await _session.Start(lifetime, lobby.ServerUrl, lobby.SessionId);

            await _playerFactory.Create(lifetime);
        }
    }
}