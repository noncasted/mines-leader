using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;

namespace GamePlay.Loop
{
    public interface ISingleGameLoop
    {
        UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData);
    }
}