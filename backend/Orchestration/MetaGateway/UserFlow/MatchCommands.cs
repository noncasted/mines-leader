using Infrastructure;
using Meta.Matches;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Microsoft.Extensions.Options;
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

            var response = new SharedBackendUser.MatchHistoryResponse
            {
                Matches = matches
                          .Select(m => (SharedBackendUser.Match)m.ToContext())
                          .OrderByDescending(m => m.Date)
                          .ToList()
            };

            return response;
        }
    }

    public class GetDetails : UserCommand<SharedBackendUser.MatchDetailsRequest>
    {
        public GetDetails(IOrleans orleans, IOptions<ProgressionOptions> progressionOptions)
        {
            _orleans = orleans;
            _progressionOptions = progressionOptions;
        }

        private readonly IOrleans _orleans;
        private readonly IOptions<ProgressionOptions> _progressionOptions;

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

            var opponentId = state.Participants.FirstOrDefault(p => p != userId);

            var opponentCards = state.ParticipantDecks.TryGetValue(opponentId, out var opp)
                ? opp.ToList()
                : new List<CardType>();

            var won = state.Winner == userId;
            var ratingChange = state.RatingChanges.TryGetValue(userId, out var rating) ? rating : 0;

            var progressionChange = won
                ? _progressionOptions.Value.WinExperience
                : _progressionOptions.Value.LossExperience;

            return new SharedBackendUser.MatchDetailsResponse
            {
                MatchId = request.MatchId,
                OwnCards = ownCards,
                OpponentCards = opponentCards,
                Time = state.Time,
                RatingChange = ratingChange,
                ProgressionChange = progressionChange,
                Won = won
            };
        }
    }
}