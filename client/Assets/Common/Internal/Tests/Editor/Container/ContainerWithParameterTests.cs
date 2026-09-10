using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerWithParameterTests
    {
        [Test]
        public void WithParameter_SuppliesConstructArgument()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(ConstructParameterService), ServiceLifetime.Singleton)
                .AsSelf()
                .WithParameter(typeof(int), 42);
            var container = builder.Build();

            var service = container.Resolve<ConstructParameterService>();

            Assert.AreEqual(42, service.Value);
        }

        [Test]
        public void WithParameter_SuppliesConstructorArgument()
        {
            var builder = new ContainerBuilder();
            builder.Add(typeof(CtorParameterService), ServiceLifetime.Singleton)
                .AsSelf()
                .WithParameter(typeof(string), "injected");
            var container = builder.Build();

            var service = container.Resolve<CtorParameterService>();

            Assert.AreEqual("injected", service.Name);
        }

        public class ConstructParameterService
        {
            public int Value { get; private set; }

            public void Construct(int value)
            {
                Value = value;
            }
        }

        public class CtorParameterService
        {
            public CtorParameterService(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }
    }
}
