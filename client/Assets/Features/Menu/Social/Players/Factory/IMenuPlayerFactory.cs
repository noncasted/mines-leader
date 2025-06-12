using Cysharp.Threading.Tasks;
using Internal;

namespace Menu
{
    public interface IMenuPlayerFactory
    {
        UniTask Create(IReadOnlyLifetime lifetime);
    }
}