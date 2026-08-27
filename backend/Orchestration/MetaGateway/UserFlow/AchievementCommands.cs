using Infrastructure;
using Meta.Users;
using MetaGateway.UserFlow.Commands;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow;

public static class AchievementCommandsExtensions
{
    public static IHostApplicationBuilder AddAchievementCommands(this IHostApplicationBuilder builder)
    {
        builder.AddUserCommand<AchievementCommands.GetRewardOptions>();
        builder.AddUserCommand<AchievementCommands.ClaimReward>();
        return builder;
    }
}

public static class AchievementCommands
{
    public class GetRewardOptions : UserCommand<SharedBackendUser.AchievementRewardOptionsRequest>
    {
        public GetRewardOptions(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override async Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.AchievementRewardOptionsRequest request)
        {
            var user = _orleans.CreateUserHandle(session.UserId);

            var options = await _orleans.Transactions.Run(
                () => user.Achievements.GetRewardOptions(request.Type, request.Tier));

            return new SharedBackendUser.AchievementRewardOptionsResponse
            {
                Type = request.Type,
                Tier = request.Tier,
                Options = options.ToList()
            };
        }
    }

    public class ClaimReward : UserCommand<SharedBackendUser.ClaimAchievementRewardRequest>
    {
        public ClaimReward(IOrleans orleans)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override async Task<INetworkContext> Execute(
            IUserSession session,
            SharedBackendUser.ClaimAchievementRewardRequest request)
        {
            var user = _orleans.CreateUserHandle(session.UserId);

            var claimed = await _orleans.Transactions.Run(
                () => user.Achievements.ClaimReward(request.Type, request.Tier, request.Card));

            if (claimed == false)
                return EmptyResponse.Fail($"Reward {request.Card} is not available for {request.Type} tier {request.Tier}");

            return EmptyResponse.Ok;
        }
    }
}
