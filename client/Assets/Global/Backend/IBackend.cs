using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Backend
{
    public interface IBackend
    {
        UniTask<Guid> Auth(IReadOnlyLifetime lifetime);
    }
}