using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;

namespace Menu.Social
{
    public interface IMenuSocialLoop
    {
        UniTask Start(IReadOnlyLifetime lifetime);
    }
    
    public class MenuSocialLoop : IMenuSocialLoop
    {
        private readonly IUser _user;
        private readonly INetworkSession _session;
        private readonly Matchmaking _matchmaking;
        private readonly IMenuPlayerFactory _playerFactory;

        public MenuSocialLoop(
            IUser user,
            INetworkSession session,
            Matchmaking matchmaking,
            IMenuPlayerFactory playerFactory)
        {
            _user = user;
            _session = session;
            _matchmaking = matchmaking;
            _playerFactory = playerFactory;
        }

        public async UniTask Start(IReadOnlyLifetime lifetime)
        {
            var lobby = await _matchmaking.SearchLobby(lifetime);
            await _session.Start(lifetime, lobby.ServerUrl, lobby.SessionId, _user.Id);

            await _playerFactory.Create(lifetime);
        }
    }
}