using Common.Extensions;
using Infrastructure;
using Meta.Users;

namespace Tests;

public class UserProgressionRatingTest
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
        public override string Title => "user-progression-rating";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // Create a user through the factory
            var userId = await _userFactory.Create(new UserCreateOptions());
            var user = _orleans.CreateUserHandle(userId);

            handle.Progress.Log($"User created: {userId}");
            handle.Progress.SetProgress(0.2f);

            // Initial progression should be 0
            var initialXp = await _transactions.Run(() => user.Progression.GetTotal());

            if (initialXp != 0)
                throw new Exception($"Initial XP: expected 0, got {initialXp}");

            // Initial rating should be 0
            var initialRating = await _transactions.Run(() => user.Rating.GetTotal());

            if (initialRating != 0)
                throw new Exception($"Initial rating: expected 0, got {initialRating}");

            handle.Progress.SetProgress(0.3f);

            // Add win records
            var winXp = 100;
            var winRating = 25;

            await _transactions.Run(() => user.Progression.AddRecord(
                new UserProgressionRecords.Win
                {
                    Date = DateTime.UtcNow,
                    Experience = winXp
                }));

            await _transactions.Run(() => user.Rating.AddRecord(
                new UserRatingRecords.Win
                {
                    Date = DateTime.UtcNow,
                    Rating = winRating
                }));

            handle.Progress.SetProgress(0.5f);

            // Verify after win
            var xpAfterWin = await _transactions.Run(() => user.Progression.GetTotal());

            if (xpAfterWin != winXp)
                throw new Exception($"XP after win: expected {winXp}, got {xpAfterWin}");

            var ratingAfterWin = await _transactions.Run(() => user.Rating.GetTotal());

            if (ratingAfterWin != winRating)
                throw new Exception($"Rating after win: expected {winRating}, got {ratingAfterWin}");

            handle.Progress.SetProgress(0.6f);

            // Add loss records
            var lossXp = 30;
            var lossRating = 15;

            await _transactions.Run(() => user.Progression.AddRecord(
                new UserProgressionRecords.Loss
                {
                    Date = DateTime.UtcNow,
                    Experience = lossXp
                }));

            await _transactions.Run(() => user.Rating.AddRecord(
                new UserRatingRecords.Loss
                {
                    Date = DateTime.UtcNow,
                    Rating = lossRating
                }));

            handle.Progress.SetProgress(0.8f);

            // Verify cumulative: XP always positive, rating can go down
            var totalXp = await _transactions.Run(() => user.Progression.GetTotal());
            var expectedXp = winXp + lossXp;

            if (totalXp != expectedXp)
                throw new Exception($"Total XP: expected {expectedXp}, got {totalXp}");

            var totalRating = await _transactions.Run(() => user.Rating.GetTotal());
            var expectedRating = winRating - lossRating; // Loss negates

            if (totalRating != expectedRating)
                throw new Exception($"Total rating: expected {expectedRating}, got {totalRating}");

            handle.Progress.Log($"Verified: XP={totalXp}, Rating={totalRating}");
            handle.Progress.SetProgress(1f);
        }
    }
}
