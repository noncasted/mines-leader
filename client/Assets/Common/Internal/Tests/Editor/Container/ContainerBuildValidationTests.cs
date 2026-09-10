using System;
using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerBuildValidationTests
    {
        [Test]
        public void Build_Cycle_ThrowsExceptionContainingPath()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(CycleA), ServiceLifetime.Singleton).AsSelf();
            builder.Add(typeof(CycleB), ServiceLifetime.Singleton).AsSelf();
            builder.Add(typeof(CycleC), ServiceLifetime.Singleton).AsSelf();

            var exception = Assert.Throws<Exception>(() => builder.Build());

            // Stub Build throws NotImplementedException, which must not count as cycle detection.
            Assert.IsNotInstanceOf<NotImplementedException>(exception);
            var text = exception.ToString();
            StringAssert.Contains(nameof(CycleA), text);
            StringAssert.Contains(nameof(CycleB), text);
            StringAssert.Contains(nameof(CycleC), text);
        }

        [Test]
        public void Build_SelfCycle_ThrowsExceptionContainingType()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(SelfCycle), ServiceLifetime.Singleton).AsSelf();

            var exception = Assert.Throws<Exception>(() => builder.Build());

            Assert.IsNotInstanceOf<NotImplementedException>(exception);
            StringAssert.Contains(nameof(SelfCycle), exception.ToString());
        }

        [Test]
        public void Build_MissingConstructDependency_Throws()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(NeedsMissingConstruct), ServiceLifetime.Singleton).AsSelf();

            var exception = Assert.Throws<Exception>(() => builder.Build());

            Assert.IsNotInstanceOf<NotImplementedException>(exception);
            var text = exception.ToString();
            StringAssert.Contains(nameof(NeedsMissingConstruct), text);
            StringAssert.Contains(nameof(UnregisteredDependency), text);
        }

        [Test]
        public void Build_MissingConstructorDependency_Throws()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(NeedsMissingCtor), ServiceLifetime.Singleton).AsSelf();

            var exception = Assert.Throws<Exception>(() => builder.Build());

            Assert.IsNotInstanceOf<NotImplementedException>(exception);
            var text = exception.ToString();
            StringAssert.Contains(nameof(NeedsMissingCtor), text);
            StringAssert.Contains(nameof(UnregisteredDependency), text);
        }

        public class CycleA
        {
            public void Construct(CycleB b)
            {
            }
        }

        public class CycleB
        {
            public void Construct(CycleC c)
            {
            }
        }

        public class CycleC
        {
            public void Construct(CycleA a)
            {
            }
        }

        public class SelfCycle
        {
            public void Construct(SelfCycle self)
            {
            }
        }

        public class UnregisteredDependency
        {
        }

        public class NeedsMissingConstruct
        {
            public void Construct(UnregisteredDependency dependency)
            {
            }
        }

        public class NeedsMissingCtor
        {
            public NeedsMissingCtor(UnregisteredDependency dependency)
            {
            }
        }
    }
}
