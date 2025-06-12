using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Backend
{
    public interface IBackendGetGateway
    {
        UniTask<string> Get(IReadOnlyLifetime lifetime, IGetRequest request);
    }
}