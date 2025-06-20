using Cysharp.Threading.Tasks;
using Internal;
using Meta;

namespace GamePlay.Loop
{
    public interface ISingleGameLoop
    {
        UniTask Process(IReadOnlyLifetime lifetime, SessionData sessionData);
    }
}