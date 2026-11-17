namespace Cluster.Deploy;

public interface IServiceSlicedState<TData, TSlice> : IDeploymentState<TData>
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    // Replaces the slice of this service. Calls that overlap are coalesced: the latest slice wins.
    Task SetSlice(TSlice slice);
}
