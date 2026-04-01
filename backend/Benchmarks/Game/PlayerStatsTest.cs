using Common.Extensions;
using Game.GamePlay;
using Game.Session;
using Shared;

namespace Benchmarks;

public class PlayerStatsTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage)
        {
        }

        public override string Group => TestGroups.Game;
        public override string Title => "player-stats";
        public override string MetricName => "ms";

        protected override Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            TestHealth(handle);
            handle.Progress.SetProgress(0.33f);

            TestMana(handle);
            handle.Progress.SetProgress(0.66f);

            TestMoves(handle);
            handle.Progress.SetProgress(1f);

            handle.Progress.Log("All player stats tests passed");
            return Task.CompletedTask;
        }

        private void TestHealth(ClusterTestNodeHandle handle)
        {
            var state = new ValueProperty<PlayerHealthState>(0).ForTest();
            var health = new Health(state);

            health.SetMax(10);
            health.SetCurrent(10);

            // Damage
            health.TakeDamage(3);

            if (health.Current.Value != 7)
                throw new Exception($"Health after damage: expected 7, got {health.Current.Value}");

            // Damage below zero — clamped to 0
            health.TakeDamage(100);

            if (health.Current.Value != 0)
                throw new Exception($"Health after lethal: expected 0, got {health.Current.Value}");

            // Heal
            health.SetCurrent(5);
            health.Heal(3);

            if (health.Current.Value != 8)
                throw new Exception($"Health after heal: expected 8, got {health.Current.Value}");

            // Heal above max — clamped
            health.Heal(100);

            if (health.Current.Value != 10)
                throw new Exception($"Health after overheal: expected 10, got {health.Current.Value}");

            // Negative damage throws
            var threw = false;

            try
            {
                health.TakeDamage(-1);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            if (!threw)
                throw new Exception("Negative damage should throw ArgumentException");

            // Negative heal throws
            threw = false;

            try
            {
                health.Heal(-1);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            if (!threw)
                throw new Exception("Negative heal should throw ArgumentException");

            handle.Progress.Log("Health tests passed");
        }

        private void TestMana(ClusterTestNodeHandle handle)
        {
            var state = new ValueProperty<PlayerManaState>(1).ForTest();
            var mana = new Mana(state);

            mana.SetMax(5);
            mana.Restore();

            if (mana.Current != 5)
                throw new Exception($"Mana after restore: expected 5, got {mana.Current}");

            // Use mana
            mana.Use(3);

            if (mana.Current != 2)
                throw new Exception($"Mana after use: expected 2, got {mana.Current}");

            // Use more than available — clamped to 0
            mana.Use(10);

            if (mana.Current != 0)
                throw new Exception($"Mana after overuse: expected 0, got {mana.Current}");

            // Restore to max
            mana.Restore();

            if (mana.Current != 5)
                throw new Exception($"Mana after second restore: expected 5, got {mana.Current}");

            // Set above max — clamped
            mana.SetCurrent(999);

            if (mana.Current != 5)
                throw new Exception($"Mana after overcap: expected 5, got {mana.Current}");

            // Negative use throws
            var threw = false;

            try
            {
                mana.Use(-1);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            if (!threw)
                throw new Exception("Negative mana use should throw ArgumentException");

            handle.Progress.Log("Mana tests passed");
        }

        private void TestMoves(ClusterTestNodeHandle handle)
        {
            var state = new ValueProperty<PlayerMovesState>(2).ForTest();
            var moves = new Moves(state);

            moves.SetMax(3);
            moves.Restore();

            if (moves.Left != 3)
                throw new Exception($"Moves after restore: expected 3, got {moves.Left}");

            // Use moves
            moves.OnUsed();

            if (moves.Left != 2)
                throw new Exception($"Moves after first use: expected 2, got {moves.Left}");

            moves.OnUsed();
            moves.OnUsed();

            if (moves.Left != 0)
                throw new Exception($"Moves after all used: expected 0, got {moves.Left}");

            // Use when zero — should throw
            var threw = false;

            try
            {
                moves.OnUsed();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            if (!threw)
                throw new Exception("Using move at 0 should throw InvalidOperationException");

            // Lock
            moves.Restore();
            moves.Lock();

            if (moves.Left != 0)
                throw new Exception($"Moves after lock: expected 0, got {moves.Left}");

            // Restore after lock
            moves.Restore();

            if (moves.Left != 3)
                throw new Exception($"Moves after restore from lock: expected 3, got {moves.Left}");

            handle.Progress.Log("Moves tests passed");
        }
    }
}
