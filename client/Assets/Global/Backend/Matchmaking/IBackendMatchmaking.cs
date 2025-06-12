using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Backend
{
    public interface IBackendMatchmaking
    {
        UniTask<SessionData> SearchGame(IReadOnlyLifetime lifetime);
        UniTask CancelSearch(IReadOnlyLifetime lifetime);
        UniTask<SessionData> SearchLobby(IReadOnlyLifetime lifetime);
        UniTask<SessionData> CreateGame(IReadOnlyLifetime lifetime);
    }
}