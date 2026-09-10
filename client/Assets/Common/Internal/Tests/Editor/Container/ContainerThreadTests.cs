using System;
using System.Threading;
using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerThreadTests
    {
        [Test]
        public void ContainerThread_Assert_OffMainThread_Throws()
        {
            Assert.DoesNotThrow(ContainerThread.Assert);

            var caught = RunOnBackgroundThread(ContainerThread.Assert);

            Assert.IsNotNull(caught);
            Assert.IsInstanceOf<InvalidOperationException>(caught);
            StringAssert.Contains("main-thread only", caught.Message);
        }

        [Test]
        public void Register_OffMainThread_Throws()
        {
            Assert.DoesNotThrow(ContainerThread.Assert);

            var builder = new ContainerBuilder();
            var caught = RunOnBackgroundThread(() => builder.Add(typeof(PlainService), ServiceLifetime.Singleton));

            Assert.IsNotNull(caught);
            Assert.IsInstanceOf<InvalidOperationException>(caught);
            StringAssert.Contains("main-thread only", caught.Message);
        }

        private static Exception RunOnBackgroundThread(Action action)
        {
            Exception caught = null;

            var thread = new Thread(() => {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    caught = exception;
                }
            });
            thread.IsBackground = true;
            thread.Start();

            Assert.IsTrue(thread.Join(5000), "Background thread did not finish.");
            return caught;
        }

        public class PlainService
        {
        }
    }
}