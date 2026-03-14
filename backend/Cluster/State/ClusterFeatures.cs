using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.State;

public interface IClusterFeatures
{
    IViewableProperty<bool> AcceptingConnections { get; }

    Task SetAcceptingConnections(bool accepting);
}

public class ClusterFeatures : DynamicState<ClusterFeaturesState>, IClusterFeatures
{
    public ClusterFeatures(IMessaging messaging, ILogger<ClusterFeatures> logger) : base(
        messaging,
        logger,
        new ClusterFeaturesState()
        {
            AcceptingConnections = false
        }
    )
    {
    }

    private readonly ViewableProperty<bool> _acceptingConnections = new(false);

    public IViewableProperty<bool> AcceptingConnections => _acceptingConnections;

    protected override void OnSetup(IReadOnlyLifetime lifetime)
    {
        this.View(lifetime, value =>
            {
                _acceptingConnections.Set(value.AcceptingConnections);
            }
        );
    }

    public Task SetAcceptingConnections(bool accepting)
    {
        Value.AcceptingConnections = accepting;
        return SetValue(Value);
    }
}

[GenerateSerializer]
public class ClusterFeaturesState
{
    [Id(0)]
    public bool AcceptingConnections { get; set; }

    public override string ToString()
    {
        return $"AcceptingConnections={AcceptingConnections}";
    }
}

public static class ClusterFeaturesExtensions
{
    public static IHostApplicationBuilder AddClusterFeatures(this IHostApplicationBuilder builder)
    {
        builder.Add<ClusterFeatures>()
            .As<IDynamicState<ClusterFeaturesState>>()
            .As<IClusterFeatures>()
            .As<ILocalSetupCompleted>();

        return builder;
    }
}