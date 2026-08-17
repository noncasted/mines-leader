using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IDelayRunner
    {
        UniTask RunDelay(float time);
        UniTask RunDelay(float time, IReadOnlyLifetime lifetime);
        UniTask RunDelay(float time, Action callback, IReadOnlyLifetime lifetime);
    }
}