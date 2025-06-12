using Cysharp.Threading.Tasks;
using Internal;

namespace Menu.Loop
{
    public interface IMenuLoop
    {
        UniTask<MenuResult> Process(IReadOnlyLifetime lifetime);
    }
}