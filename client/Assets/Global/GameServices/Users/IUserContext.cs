using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.GameServices
{
    public interface IUserContext
    {
        Guid Id { get; }

        UniTask Init(IReadOnlyLifetime lifetime);
    }
}