using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.Matchmaking;

public class MatchmakingCommands
{
    public class SearchLobby : UserCommand<SharedMatchmaking.SearchLobby>
    {
        public SearchLobby(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedMatchmaking.SearchLobby request)
        {
            return await _matchmaking.SearchLobby(session.UserId);
        }
    }

    public class SearchMatch : UserCommand<SharedMatchmaking.SearchMatch>
    {
        public SearchMatch(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override Task<INetworkContext> Execute(IUserSession session, SharedMatchmaking.SearchMatch request)
        {
            if (request.RequestId == Guid.Empty)
                throw new ArgumentException("A match search without a request id");

            return _matchmaking.SearchMatch(session.UserId, request.Type, request.RequestId).FromResult();
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
            return _matchmaking.CancelMatchSearch(session.UserId).FromResult();
        }
    }

    public class Create : UserCommand<SharedMatchmaking.Create>
    {
        public Create(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedMatchmaking.Create request)
        {
            return await _matchmaking.Create(session.UserId, request.Type);
        }
    }

    public class CreateWithBot : UserCommand<SharedMatchmaking.CreateWithBot>
    {
        public CreateWithBot(IMatchmaking matchmaking)
        {
            _matchmaking = matchmaking;
        }

        private readonly IMatchmaking _matchmaking;

        protected override async Task<INetworkContext> Execute(IUserSession session, SharedMatchmaking.CreateWithBot request)
        {
            return await _matchmaking.CreateWithBot(session.UserId, request.Type, request.Fixture);
        }
    }
}