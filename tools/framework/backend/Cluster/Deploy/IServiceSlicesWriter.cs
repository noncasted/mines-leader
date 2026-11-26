using Infrastructure;

namespace Cluster.Deploy;

public interface IServiceSlicesWriter<TData, TSlice> : IGrainWithGuidKey
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    Task<AddressableStateValue> SetSlice(Guid serviceId, TSlice slice);
}
