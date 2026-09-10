using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerConstructTests
    {
        [Test]
        public void Construct_RegisteredDependency_IsInjected()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(InjectedDependency), ServiceLifetime.Singleton).AsSelf();
            builder.Add(typeof(ExistingTarget), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var service = container.Resolve<ExistingTarget>();

            Assert.IsNotNull(service.Dependency);
            Assert.AreSame(container.Resolve<InjectedDependency>(), service.Dependency);
        }

        [Test]
        public void AddInjection_ConstructsExistingObject()
        {
            var target = new ExistingTarget();
            var builder = new ContainerBuilder();
            builder.Add(typeof(InjectedDependency), ServiceLifetime.Singleton).AsSelf();
            builder.AddInjection(target);
            builder.Build();

            Assert.IsNotNull(target.Dependency);
            Assert.IsInstanceOf<InjectedDependency>(target.Dependency);
        }

        [Test]
        public void Inject_ConstructsExistingObject()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(InjectedDependency), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var target = new ExistingTarget();
            container.Inject(target);

            Assert.IsNotNull(target.Dependency);
            Assert.AreSame(container.Resolve<InjectedDependency>(), target.Dependency);
        }

        [Test]
        public void Construct_ZeroParameters_IsCalled()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(ZeroConstructService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var service = container.Resolve<ZeroConstructService>();

            Assert.IsTrue(service.ConstructCalled);
        }

        [Test]
        public void TypeWithoutConstruct_IsCreatedViaConstructor()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(InjectedDependency), ServiceLifetime.Singleton).AsSelf();
            builder.Add(typeof(CtorOnlyService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var service = container.Resolve<CtorOnlyService>();

            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Dependency);
            Assert.AreSame(container.Resolve<InjectedDependency>(), service.Dependency);
        }

        public class InjectedDependency
        {
        }

        public class ExistingTarget
        {
            public InjectedDependency Dependency { get; private set; }

            public void Construct(InjectedDependency dependency)
            {
                Dependency = dependency;
            }
        }

        public class ZeroConstructService
        {
            public bool ConstructCalled { get; private set; }

            public void Construct()
            {
                ConstructCalled = true;
            }
        }

        public class CtorOnlyService
        {
            public CtorOnlyService(InjectedDependency dependency)
            {
                Dependency = dependency;
            }

            public InjectedDependency Dependency { get; }
        }
    }
}
