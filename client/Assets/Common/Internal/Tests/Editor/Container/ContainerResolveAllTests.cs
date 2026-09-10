using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerResolveAllTests
    {
        [Test]
        public void ResolveAll_PreservesRegistrationOrder()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(OrderedFirst), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(IOrderedService));
            builder.Add(typeof(OrderedSecond), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(IOrderedService));
            builder.Add(typeof(OrderedThird), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(IOrderedService));
            var container = builder.Build();

            var all = container.ResolveAll<IOrderedService>();

            Assert.IsNotNull(all);
            Assert.AreEqual(3, all.Count);
            Assert.IsInstanceOf<OrderedFirst>(all[0]);
            Assert.IsInstanceOf<OrderedSecond>(all[1]);
            Assert.IsInstanceOf<OrderedThird>(all[2]);
            Assert.AreSame(all[0], container.Resolve<OrderedFirst>());
            Assert.AreSame(all[2], container.Resolve<OrderedThird>());
        }

        [Test]
        public void ResolveAll_Empty_ReturnsEmptyList()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(OrderedFirst), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var all = container.ResolveAll<IEmptyMarker>();

            Assert.IsNotNull(all);
            Assert.AreEqual(0, all.Count);
        }

        [Test]
        public void ResolveAll_EmptyContainer_ReturnsEmptyList()
        {
            var container = new ContainerBuilder().Build();

            var all = container.ResolveAll<IEmptyMarker>();

            Assert.IsNotNull(all);
            Assert.AreEqual(0, all.Count);
        }

        public interface IOrderedService
        {
        }

        public class OrderedFirst : IOrderedService
        {
        }

        public class OrderedSecond : IOrderedService
        {
        }

        public class OrderedThird : IOrderedService
        {
        }

        public interface IEmptyMarker
        {
        }
    }
}
