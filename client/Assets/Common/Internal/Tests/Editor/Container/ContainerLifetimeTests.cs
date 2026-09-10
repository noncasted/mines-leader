using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerLifetimeTests
    {
        [Test]
        public void Resolve_Singleton_ReturnsSameInstance()
        {
            CountingSingleton.Instances = 0;

            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingSingleton), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var first = container.Resolve<CountingSingleton>();
            var second = container.Resolve<CountingSingleton>();
            var untyped = container.Resolve(typeof(CountingSingleton));

            Assert.AreSame(first, second);
            Assert.AreSame(first, untyped);
            Assert.AreEqual(1, CountingSingleton.Instances);
        }

        [Test]
        public void Build_Singleton_ConstructsEagerly()
        {
            CountingSingleton.Instances = 0;

            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingSingleton), ServiceLifetime.Singleton).AsSelf();
            builder.Build();

            Assert.AreEqual(1, CountingSingleton.Instances);
        }

        [Test]
        public void Resolve_Scoped_ReturnsSameInstanceWithinContainer()
        {
            CountingScoped.Instances = 0;

            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingScoped), ServiceLifetime.Scoped).AsSelf();
            var container = builder.Build();

            var first = container.Resolve<CountingScoped>();
            var second = container.Resolve<CountingScoped>();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, CountingScoped.Instances);
        }

        [Test]
        public void Build_Scoped_ConstructsEagerly()
        {
            CountingScoped.Instances = 0;

            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingScoped), ServiceLifetime.Scoped).AsSelf();
            builder.Build();

            Assert.AreEqual(1, CountingScoped.Instances);
        }

        [Test]
        public void Resolve_Scoped_ReturnsDifferentInstanceInChild()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingScoped), ServiceLifetime.Scoped).AsSelf();
            var parent = builder.Build();
            var child = parent.CreateChild().Build();

            var parentInstance = parent.Resolve<CountingScoped>();
            var childInstance = child.Resolve<CountingScoped>();
            var childAgain = child.Resolve<CountingScoped>();

            Assert.AreNotSame(parentInstance, childInstance);
            Assert.AreSame(childInstance, childAgain);
        }

        [Test]
        public void Resolve_Transient_ReturnsNewInstanceEachTime()
        {
            CountingTransient.Instances = 0;

            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingTransient), ServiceLifetime.Transient).AsSelf();
            var container = builder.Build();

            var first = container.Resolve<CountingTransient>();
            var second = container.Resolve<CountingTransient>();

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, CountingTransient.Instances);
        }

        [Test]
        public void Build_Transient_DoesNotConstructUntilResolve()
        {
            CountingTransient.Instances = 0;

            var builder = new ContainerBuilder();
            builder.Add(typeof(CountingTransient), ServiceLifetime.Transient).AsSelf();
            builder.Build();

            Assert.AreEqual(0, CountingTransient.Instances);
        }

        public class CountingSingleton
        {
            public static int Instances;

            public CountingSingleton()
            {
                Instances++;
            }
        }

        public class CountingScoped
        {
            public static int Instances;

            public CountingScoped()
            {
                Instances++;
            }
        }

        public class CountingTransient
        {
            public static int Instances;

            public CountingTransient()
            {
                Instances++;
            }
        }
    }
}
