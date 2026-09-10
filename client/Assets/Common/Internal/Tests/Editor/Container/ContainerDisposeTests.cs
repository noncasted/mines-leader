using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerDisposeTests
    {
        [Test]
        public void Dispose_Order_IsReverseOfBuildOrder()
        {
            var log = new DisposeLog();
            var builder = new ContainerBuilder();
            builder.AddInstance(typeof(DisposeLog), log);
            // Registration order is C → B → A; BuildOrder is A → B → C.
            builder.Add(typeof(DisposeC), ServiceLifetime.Singleton).AsSelf();
            builder.Add(typeof(DisposeB), ServiceLifetime.Singleton).AsSelf();
            builder.Add(typeof(DisposeA), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            container.Dispose();

            CollectionAssert.AreEqual(
                new[] { nameof(DisposeC), nameof(DisposeB), nameof(DisposeA) },
                log.Order);
        }

        [Test]
        public void Dispose_TerminatesContainerLifetime()
        {
            var container = new ContainerBuilder().Build();

            Assert.IsNotNull(container.Lifetime);
            Assert.IsFalse(container.Lifetime.IsTerminated);

            var terminated = false;
            container.Lifetime.Listen(() => terminated = true);
            container.Dispose();

            Assert.IsTrue(terminated);
            Assert.IsTrue(container.Lifetime.IsTerminated);
        }

        [Test]
        public void Dispose_Parent_TerminatesChildLifetime()
        {
            var parent = new ContainerBuilder("parent").Build();
            var child = parent.CreateChild().Build();

            Assert.IsFalse(parent.Lifetime.IsTerminated);
            Assert.IsFalse(child.Lifetime.IsTerminated);

            var childTerminated = false;
            child.Lifetime.Listen(() => childTerminated = true);
            parent.Dispose();

            Assert.IsTrue(parent.Lifetime.IsTerminated);
            Assert.IsTrue(child.Lifetime.IsTerminated);
            Assert.IsTrue(childTerminated);
        }

        [Test]
        public void Dispose_Child_DoesNotTerminateParentLifetime()
        {
            var parent = new ContainerBuilder("parent").Build();
            var child = parent.CreateChild().Build();

            child.Dispose();

            Assert.IsTrue(child.Lifetime.IsTerminated);
            Assert.IsFalse(parent.Lifetime.IsTerminated);
        }

        public class DisposeLog
        {
            public List<string> Order { get; } = new List<string>();
        }

        public class DisposeA : IDisposable
        {
            private readonly DisposeLog _log;

            public DisposeA(DisposeLog log)
            {
                _log = log;
            }

            public void Dispose()
            {
                _log.Order.Add(nameof(DisposeA));
            }
        }

        public class DisposeB : IDisposable
        {
            private readonly DisposeLog _log;

            public DisposeB(DisposeLog log, DisposeA a)
            {
                _log = log;
            }

            public void Dispose()
            {
                _log.Order.Add(nameof(DisposeB));
            }
        }

        public class DisposeC : IDisposable
        {
            private readonly DisposeLog _log;

            public DisposeC(DisposeLog log, DisposeB b)
            {
                _log = log;
            }

            public void Dispose()
            {
                _log.Order.Add(nameof(DisposeC));
            }
        }
    }
}
