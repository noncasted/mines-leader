using Backend.Gateway;
using Shared;

namespace Backend.Matches;

public class MatchmakingCommands
{
    public class Search : UserCommand<SharedMatchmaking.Search>
    {
        public Search(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(IUserSession session, SharedMatchmaking.Search request)
        {
            return _matchmaking.Search(session.UserId, request.Type).FromResult();
        }
    }

    public class CancelSearch : UserCommand<SharedMatchmaking.CancelSearch>
    {
        public CancelSearch(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(
            IUserSession session,
            SharedMatchmaking.CancelSearch request)
        {
            return _matchmaking.CancelSearch(session.UserId).FromResult();
        }
    }

    public class Create : UserCommand<SharedMatchmaking.Create>
    {
        public Create(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(IUserSession session, SharedMatchmaking.Create request)
        {
            return _matchmaking.Create(session.UserId).FromResult();
        }
    }
}