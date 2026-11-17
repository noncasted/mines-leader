using Infrastructure;
using Meta.Matches;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow;

public static class MatchCommandsExtensions
{
    public static IHostApplicationBuilder AddMatchCommands(this IHostApplicationBuilder builder)
    {
        builder.AddUserCommand<MatchCommands.GetHistory>();
        builder.AddUserCommand<MatchCommands.GetDetails>();
        return builder;
    }
}

public static class MatchCommands
{
    public class GetHistory : UserCommand<SharedBackendUser.MatchHistoryRequest>
    {
        public GetHistory(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override async Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.MatchHistoryRequest request)
        {
            var user = _orleans.CreateUserHandle(session.UserId);
            var count = request.Count > 0 ? request.Count : 30;

            var matches = await _orleans.Transactions.Run(() => user.MatchHistory.GetBlock(count));

            var contexts = matches
                           .Select(m => (SharedBackendUser.Match)m.ToContext())
                           .OrderByDescending(m => m.Date)
                           .ToList();

            var names = await ResolveNames(contexts.Select(m => OpponentOf(m.Participants, session.UserId)));

            foreach (var match in contexts)
                match.OpponentName = names.GetValueOrDefault(OpponentOf(match.Participants, session.UserId), string.Empty);

            return new SharedBackendUser.MatchHistoryResponse
            {
                Matches = contexts
            };
        }

        /// <summary>Имена соперников читаются одной пачкой: в истории они повторяются.</summary>
        private async Task<Dictionary<Guid, string>> ResolveNames(IEnumerable<Guid> ids)
        {
            var unique = ids.Where(id => id != Guid.Empty).Distinct().ToList();

            if (unique.Count == 0)
                return new Dictionary<Guid, string>();

            var states = await _orleans.Transactions.Run(() => Task.WhenAll(
                unique.Select(id => _orleans.CreateUserHandle(id).Entity.GetState())));

            return unique
                   .Zip(states, (id, state) => (id, state.Name))
                   .ToDictionary(entry => entry.id, entry => entry.Name);
        }
    }

    public class GetDetails : UserCommand<SharedBackendUser.MatchDetailsRequest>
    {
        public GetDetails(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override async Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.MatchDetailsRequest request)
        {
            var match = _orleans.GetGrain<IMatch>(request.MatchId);
            var state = await _orleans.Transactions.Run(match.GetState);

            var userId = session.UserId;

            var ownCards = state.ParticipantDecks.TryGetValue(userId, out var own)
                ? own.ToList()
                : new List<CardType>();

            var opponentId = OpponentOf(state.Participants, userId);

            var opponentCards = state.ParticipantDecks.TryGetValue(opponentId, out var opp)
                ? opp.ToList()
                : new List<CardType>();

            var opponentName = string.Empty;

            if (opponentId != Guid.Empty)
            {
                var opponentState = await _orleans.Transactions.Run(
                    () => _orleans.CreateUserHandle(opponentId).Entity.GetState());

                opponentName = opponentState.Name;
            }

            var won = state.Winner == userId;
            var ratingChange = state.RatingChanges.TryGetValue(userId, out var rating) ? rating : 0;

            return new SharedBackendUser.MatchDetailsResponse
            {
                MatchId = request.MatchId,
                OwnCards = ownCards,
                OpponentCards = opponentCards,
                Time = state.Time,
                RatingChange = ratingChange,
                Won = won,
                OpponentName = opponentName,
                Date = state.StartDate,
                Type = state.Type
            };
        }
    }

    private static Guid OpponentOf(IReadOnlyList<Guid>? participants, Guid userId)
    {
        return participants == null ? Guid.Empty : participants.FirstOrDefault(p => p != userId);
    }
}
