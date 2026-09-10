using System;
using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerResolveTests
    {
        [Test]
        public void TryResolve_MissingType_ReturnsFalse()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(RegisteredService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var found = container.TryResolve(typeof(UnregisteredService), out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryResolve_RegisteredType_ReturnsTrue()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(RegisteredService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            var found = container.TryResolve(typeof(RegisteredService), out var instance);

            Assert.IsTrue(found);
            Assert.IsInstanceOf<RegisteredService>(instance);
            Assert.AreSame(container.Resolve<RegisteredService>(), instance);
        }

        [Test]
        public void Resolve_MissingTypeAfterSuccessfulBuild_Throws()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(RegisteredService), ServiceLifetime.Singleton).AsSelf();
            var container = builder.Build();

            Assert.Throws<Exception>(() => container.Resolve<UnregisteredService>());
            Assert.Throws<Exception>(() => container.Resolve(typeof(UnregisteredService)));
        }

        public class RegisteredService
        {
        }

        public class UnregisteredService
        {
        }
    }
}
