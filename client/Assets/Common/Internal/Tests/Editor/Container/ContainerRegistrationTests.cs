using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerRegistrationTests
    {
        [Test]
        public void Resolve_As_ReturnsImplementationForServiceType()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(AsImplementation), ServiceLifetime.Singleton)
                .As(typeof(IAsService));
            var container = builder.Build();

            var resolved = container.Resolve<IAsService>();

            Assert.IsInstanceOf<AsImplementation>(resolved);
        }

        [Test]
        public void Resolve_AsSelf_ReturnsImplementation()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(AsSelfService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var resolved = container.Resolve<AsSelfService>();

            Assert.IsNotNull(resolved);
            Assert.IsInstanceOf<AsSelfService>(resolved);
        }

        [Test]
        public void Resolve_ServiceTypeCollision_LastRegistrationWins()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(FirstCollision), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(ICollisionService));
            builder.Add(typeof(SecondCollision), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(ICollisionService));
            var container = builder.Build();

            var resolved = container.Resolve<ICollisionService>();

            Assert.IsInstanceOf<SecondCollision>(resolved);
            Assert.IsInstanceOf<FirstCollision>(container.Resolve<FirstCollision>());
            Assert.IsInstanceOf<SecondCollision>(container.Resolve<SecondCollision>());
        }

        [Test]
        public void AddInstance_ReturnsProvidedInstance()
        {
            var instance = new InstanceService();
            var builder = new ContainerBuilder();
            builder.AddInstance(typeof(IInstanceService), instance);
            var container = builder.Build();

            Assert.AreSame(instance, container.Resolve<IInstanceService>());
            Assert.AreSame(instance, container.Resolve(typeof(IInstanceService)));
        }

        [Test]
        public void AddInstance_AsAdditionalServiceType_IsResolvable()
        {
            var instance = new InstanceService();
            var builder = new ContainerBuilder();
            builder.AddInstance(typeof(InstanceService), instance)
                .As(typeof(IInstanceService));
            var container = builder.Build();

            Assert.AreSame(instance, container.Resolve<InstanceService>());
            Assert.AreSame(instance, container.Resolve<IInstanceService>());
        }

        [Test]
        public void AddSelfResolvable_MakesImplementationResolvable()
        {
            var builder = new ContainerBuilder();
            var registration = builder.Add(typeof(SelfResolvableService), ServiceLifetime.Singleton)
                .AsSelf()
                .As(typeof(ISelfResolvableService));
            builder.AddSelfResolvable(registration);
            var container = builder.Build();

            var asInterface = container.Resolve<ISelfResolvableService>();
            var asSelf = container.Resolve<SelfResolvableService>();

            Assert.IsInstanceOf<SelfResolvableService>(asInterface);
            Assert.AreSame(asInterface, asSelf);
        }

        [Test]
        public void AddSelfResolvable_Transient_IsConstructedOnBuild()
        {
            SelfResolvableTransient.Instances = 0;

            var builder = new ContainerBuilder();
            var registration = builder.Add(typeof(SelfResolvableTransient), ServiceLifetime.Transient)
                .AsSelf();
            builder.AddSelfResolvable(registration);
            builder.Build();

            Assert.AreEqual(1, SelfResolvableTransient.Instances);
        }

        [Test]
        public void Resolve_IContainer_ReturnsSelf()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(AsSelfService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            Assert.AreSame(container, container.Resolve<IContainer>());
            Assert.AreSame(container, container.Resolve(typeof(IContainer)));
        }

        [Test]
        public void Resolve_IContainer_ChildReturnsChildNotParent()
        {
            var parentBuilder = new ContainerBuilder("parent");
            parentBuilder.Add(typeof(AsSelfService), ServiceLifetime.Singleton).AsSelf();
            var parent = parentBuilder.Build();
            var child = parent.CreateChild().Build();

            Assert.AreSame(parent, parent.Resolve<IContainer>());
            Assert.AreSame(child, child.Resolve<IContainer>());
            Assert.AreNotSame(parent, child.Resolve<IContainer>());
        }

        public interface IAsService
        {
        }

        public class AsImplementation : IAsService
        {
        }

        public class AsSelfService
        {
        }

        public interface ICollisionService
        {
        }

        public class FirstCollision : ICollisionService
        {
        }

        public class SecondCollision : ICollisionService
        {
        }

        public interface IInstanceService
        {
        }

        public class InstanceService : IInstanceService
        {
        }

        public interface ISelfResolvableService
        {
        }

        public class SelfResolvableService : ISelfResolvableService
        {
        }

        public class SelfResolvableTransient
        {
            public static int Instances;

            public SelfResolvableTransient()
            {
                Instances++;
            }
        }
    }
}
