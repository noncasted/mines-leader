using Common.Extensions;
using Infrastructure;
using Meta.Matches;
using Meta.Users;
using Shared;

namespace Tests;

public class MatchRecordingTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions, IUserFactory userFactory)
            : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
            _userFactory = userFactory;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;
        private readonly IUserFactory _userFactory;

        public override string Group => TestGroups.Meta;
        public override string Title => "match-recording";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // Create two users
            var user1Id = await _userFactory.Create(new UserCreateOptions());
            var user2Id = await _userFactory.Create(new UserCreateOptions());
            Cleanup.TrackUser(user1Id);
            Cleanup.TrackUser(user2Id);
            var user1 = _orleans.CreateUserHandle(user1Id);
            var user2 = _orleans.CreateUserHandle(user2Id);

            handle.Progress.Log($"Users created: {user1Id}, {user2Id}");
            handle.Progress.SetProgress(0.2f);

            // Create and setup match
            var matchId = Guid.NewGuid();
            Cleanup.TrackMatch(matchId);
            var matchGrain = _orleans.GetGrain<IMatch>(matchId);
            var participants = new List<Guid> { user1Id, user2Id };

            await _transactions.Run(() =>
                matchGrain.Setup(GameMatchType.TimeLimited, participants));

            handle.Progress.Log("Match setup complete");
            handle.Progress.SetProgress(0.4f);

            // Verify match state after setup
            var matchState = await _transactions.Run(() => matchGrain.GetState());

            if (matchState.Participants.Count != 2)
                throw new Exception($"Match participants: expected 2, got {matchState.Participants.Count}");

            if (matchState.Type != GameMatchType.TimeLimited)
                throw new Exception($"Match type: expected Default, got {matchState.Type}");

            handle.Progress.SetProgress(0.5f);

            // Complete the match — user1 wins
            await _transactions.Run(() => matchGrain.OnComplete(user1Id));

            handle.Progress.Log("Match completed");
            handle.Progress.SetProgress(0.7f);

            // Verify match state after completion
            var completedState = await _transactions.Run(() => matchGrain.GetState());

            if (completedState.Winner != user1Id)
                throw new Exception($"Winner: expected {user1Id}, got {completedState.Winner}");

            handle.Progress.SetProgress(0.8f);

            // Verify progression was applied to both users
            var user1Xp = await _transactions.Run(() => user1.Progression.GetTotal());
            var user2Xp = await _transactions.Run(() => user2.Progression.GetTotal());

            if (user1Xp <= 0)
                throw new Exception($"Winner XP should be positive, got {user1Xp}");

            if (user2Xp <= 0)
                throw new Exception($"Loser XP should be positive (participation), got {user2Xp}");

            // Verify rating changes
            var user1Rating = await _transactions.Run(() => user1.Rating.GetTotal());
            var user2Rating = await _transactions.Run(() => user2.Rating.GetTotal());

            if (user1Rating <= 0)
                throw new Exception($"Winner rating should be positive, got {user1Rating}");

            if (user2Rating >= 0)
                throw new Exception($"Loser rating should be negative, got {user2Rating}");

            // Verify match history
            var user1History = await _transactions.Run(() => user1.MatchHistory.GetBlock(10));

            if (user1History.Count == 0)
                throw new Exception("Winner should have match in history");

            var user2History = await _transactions.Run(() => user2.MatchHistory.GetBlock(10));

            if (user2History.Count == 0)
                throw new Exception("Loser should have match in history");

            handle.Progress.Log(
                $"Match verified: winner XP={user1Xp} rating={user1Rating}, " +
                $"loser XP={user2Xp} rating={user2Rating}");
            handle.Progress.SetProgress(1f);
        }
    }
}
