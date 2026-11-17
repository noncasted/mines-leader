using NUnit.Framework;

namespace GamePlay.Metrics.Tests
{
    public class MatchClockTests
    {
        [Test]
        public void StandsAtZeroBeforeTheFirstStamp()
        {
            var clock = new MatchClock();

            Assert.That(clock.SecondsAt(100d), Is.EqualTo(0f));

            // MatchMs is 0 until the server starts the game: no stamp.
            clock.Receive(0, 100d);
            Assert.That(clock.SecondsAt(105d), Is.EqualTo(0f));
        }

        [Test]
        public void GoesOnWithTheRealTimeFromTheLastStamp()
        {
            var clock = new MatchClock();
            clock.Receive(2000, 10d);

            Assert.That(clock.SecondsAt(10d), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(clock.SecondsAt(11.5d), Is.EqualTo(3.5f).Within(1e-4f));

            clock.Receive(5000, 12d);
            Assert.That(clock.SecondsAt(13d), Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void NeverGoesBack()
        {
            var clock = new MatchClock();
            clock.Receive(1000, 0d);
            Assert.That(clock.SecondsAt(3d), Is.EqualTo(4f).Within(1e-4f));

            // A stamp behind the extrapolation (a late snapshot): the clock stands until it catches up.
            clock.Receive(2000, 3d);
            Assert.That(clock.SecondsAt(3d), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(clock.SecondsAt(4d), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(clock.SecondsAt(5.5d), Is.EqualTo(4.5f).Within(1e-4f));
        }

        [Test]
        public void StopsAtTheCompletion()
        {
            var clock = new MatchClock();
            clock.Receive(60000, 100d);
            clock.Freeze(100d);

            Assert.That(clock.SecondsAt(200d), Is.EqualTo(60f).Within(1e-4f));

            // The server clock goes on after the end: the stamps are ignored.
            clock.Receive(90000, 130d);
            Assert.That(clock.SecondsAt(300d), Is.EqualTo(60f).Within(1e-4f));
        }
    }
}
