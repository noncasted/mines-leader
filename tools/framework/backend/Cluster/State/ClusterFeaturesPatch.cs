namespace Cluster.State;

[GenerateSerializer]
public class ClusterFeaturesPatch
{
    [Id(0)] public bool? AcceptingConnections { get; init; }
    [Id(1)] public bool? SideEffectsEnabled { get; init; }
    [Id(2)] public bool? CoordinatorUnhealthy { get; init; }
    [Id(3)] public Dictionary<string, bool>? GameFlags { get; init; }

    public void ApplyTo(ClusterFeaturesState state)
    {
        if (AcceptingConnections is { } accepting)
            state.SetAcceptingConnections(accepting);

        if (SideEffectsEnabled is { } sideEffects)
            state.SetSideEffectsEnabled(sideEffects);

        if (CoordinatorUnhealthy is { } unhealthy)
            state.SetCoordinatorUnhealthy(unhealthy);

        if (GameFlags != null)
        {
            foreach (var (name, enabled) in GameFlags)
                state.SetGameFlag(name, enabled);
        }
    }

    public override string ToString()
    {
        return $"AcceptingConnections={AcceptingConnections?.ToString() ?? "-"}, " +
               $"SideEffects={SideEffectsEnabled?.ToString() ?? "-"}, " +
               $"CoordinatorUnhealthy={CoordinatorUnhealthy?.ToString() ?? "-"}, " +
               $"GameFlags={(GameFlags == null ? "-" : string.Join(",", GameFlags.Select(f => $"{f.Key}={f.Value}")))}";
    }
}
