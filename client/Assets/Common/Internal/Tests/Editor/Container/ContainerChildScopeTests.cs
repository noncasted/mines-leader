using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerChildScopeTests
    {
        [Test]
        public void Child_Resolve_ParentSingleton_ReturnsSameInstance()
        {
            var parentBuilder = new ContainerBuilder("parent");
            parentBuilder.Add(typeof(ParentSingleton), ServiceLifetime.Singleton).AsSelf();
            var parent = parentBuilder.Build();
            var child = parent.CreateChild().Build();

            var fromParent = parent.Resolve<ParentSingleton>();
            var fromChild = child.Resolve<ParentSingleton>();

            Assert.AreSame(fromParent, fromChild);
            Assert.AreSame(fromChild, child.Resolve<ParentSingleton>());
        }

        [Test]
        public void Child_Resolve_ParentRegisteredType_Succeeds()
        {
            var parentBuilder = new ContainerBuilder("parent");
            parentBuilder.Add(typeof(ParentSingleton), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(IParentService));
            var parent = parentBuilder.Build();
            var child = parent.CreateChild().Build();

            var resolved = child.Resolve<IParentService>();

            Assert.IsInstanceOf<ParentSingleton>(resolved);
            Assert.AreSame(parent.Resolve<IParentService>(), resolved);
        }

        [Test]
        public void Child_Override_ServiceType_Wins()
        {
            var parentBuilder = new ContainerBuilder("parent");
            parentBuilder.Add(typeof(ParentOverride), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(IOverridable));
            var parent = parentBuilder.Build();

            var childBuilder = parent.CreateChild();
            childBuilder.Add(typeof(ChildOverride), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(IOverridable));
            var child = childBuilder.Build();

            Assert.IsInstanceOf<ChildOverride>(child.Resolve<IOverridable>());
            Assert.IsInstanceOf<ParentOverride>(parent.Resolve<IOverridable>());
            Assert.AreNotSame(parent.Resolve<IOverridable>(), child.Resolve<IOverridable>());
            Assert.AreEqual(1, parent.ResolveAll<IOverridable>().Count);
            Assert.IsInstanceOf<ParentOverride>(parent.ResolveAll<IOverridable>()[0]);
        }

        public interface IParentService
        {
        }

        public class ParentSingleton : IParentService
        {
        }

        public interface IOverridable
        {
        }

        public class ParentOverride : IOverridable
        {
        }

        public class ChildOverride : IOverridable
        {
        }
    }
}
