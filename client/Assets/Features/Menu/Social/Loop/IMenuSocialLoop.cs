using Cysharp.Threading.Tasks;
using Internal;

namespace Menu
{
    public interface IMenuSocialLoop
    {
        UniTask Start(IReadOnlyLifetime lifetime);
    }
}