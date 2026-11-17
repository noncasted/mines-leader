using Cluster.Deploy;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster;

// The stored and published names of the deployment state and its registration.
public class DeploymentStateApiTests
{
    public class Marker
    {
        public int Value { get; init; }
    }

    private static readonly Guid DeployId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string MarkerFullName = "Framework.Tests.Cluster.DeploymentStateApiTests+Marker";

    [Fact]
    public void ChannelIds_RawNames()
    {
        new DeploymentStateChannelId<Marker>(DeployId).ToRaw()
            .Should().Be($"deployment-state-{MarkerFullName}-11111111222233334444555555555555");
    }

    [Fact]
    public void Identities_StoredNames()
    {
        var byDeploy = DeploymentState<Marker>.CreateDefaultStateIdentity(DeployId);
        byDeploy.Key.Should().Be(DeployId);
        byDeploy.Type.Should().Be("deployment_state_Marker");
        byDeploy.TableName.Should().Be("cluster");
        byDeploy.Extension.Should().BeNull();
    }

    [Fact]
    public async Task SetValue_BeforeFirstDeploy_Throws()
    {
        var state = new DeploymentState<Marker>(
            CreateOrleans(Substitute.For<IStateStorage>()),
            Substitute.For<IMessaging>(),
            NullLoggerFactory.Instance,
            new Marker());

        var set = () => state.SetValue(new Marker());

        await set.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not initialized*");
        state.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void AddDeploymentState_RegistersStateAndItsInterfaces()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.AddDeploymentState<Marker>().Should().BeSameAs(builder);

        ShouldHaveDeploymentState(builder);
    }

    private static void ShouldHaveDeploymentState(IHostApplicationBuilder builder)
    {
        var types = builder.Services.Select(descriptor => descriptor.ServiceType).ToList();

        types.Should().Contain(typeof(Marker));
        types.Should().Contain(typeof(DeploymentState<Marker>));
        types.Should().Contain(typeof(IDeploymentState<Marker>));
        types.Should().Contain(typeof(IDeployAware));
    }

    private static IOrleans CreateOrleans(IStateStorage storage)
    {
        var orleans = Substitute.For<IOrleans>();
        orleans.StateStorage.Returns(storage);
        orleans.Serializer.Returns(new JsonTestSerializer());
        return orleans;
    }
}
