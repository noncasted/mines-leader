using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IAnimation
    {
        void Play(IReadOnlyLifetime lifetime, float time = 0);
        UniTask PlayAsync(IReadOnlyLifetime lifetime, float time = 0);
        void PlayLooped(IReadOnlyLifetime lifetime, float time = 0);
    }
}