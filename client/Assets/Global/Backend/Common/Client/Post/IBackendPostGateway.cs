using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Backend
{
    public interface IBackendPostGateway
    {
        UniTask<T> Post<T>(IReadOnlyLifetime lifetime, IPostRequest request);
        UniTask Post(IReadOnlyLifetime lifetime, IPostRequest request);
    }
}