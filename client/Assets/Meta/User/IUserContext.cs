using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Assets.Meta
{
    public interface IUserContext
    {
        Guid Id { get; }

        UniTask Init(IReadOnlyLifetime lifetime);
    }
}