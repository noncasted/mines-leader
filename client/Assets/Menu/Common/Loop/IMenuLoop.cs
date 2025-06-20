using Cysharp.Threading.Tasks;
using Internal;

namespace Menu.Common
{
    public interface IMenuLoop
    {
        UniTask<MenuResult> Process(IReadOnlyLifetime lifetime);
    }
}