using Backend.Gateway;
using Shared;

namespace Backend.Matches;

public class MatchmakingCommands
{
    public class Search : UserCommand<MatchmakingContexts.Search>
    {
        public Search(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(IUserSession session, MatchmakingContexts.Search request)
        {
            return _matchmaking.Search(session.UserId, request.Type).FromResult();
        }
    }

    public class CancelSearch : UserCommand<MatchmakingContexts.CancelSearch>
    {
        public CancelSearch(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(
            IUserSession session,
            MatchmakingContexts.CancelSearch request)
        {
            return _matchmaking.CancelSearch(session.UserId).FromResult();
        }
    }

    public class Create : UserCommand<MatchmakingContexts.Create>
    {
        public Create(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(IUserSession session, MatchmakingContexts.Create request)
        {
            return _matchmaking.Create(session.UserId).FromResult();
        }
    }
}