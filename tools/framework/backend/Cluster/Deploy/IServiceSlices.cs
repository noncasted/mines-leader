namespace Cluster.Deploy;

// Deployment state assembled from per-service parts: every service owns the slice under its service id
// and writes only it, so two gateways never overwrite each other's data.
public interface IServiceSlices<TSlice> where TSlice : class
{
    IReadOnlyDictionary<Guid, TSlice> Slices { get; }

    void SetSlice(Guid serviceId, TSlice slice);
    void RemoveSlice(Guid serviceId);
}
