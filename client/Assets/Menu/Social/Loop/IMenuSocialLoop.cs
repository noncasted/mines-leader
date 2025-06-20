using Cysharp.Threading.Tasks;
using Internal;

namespace Menu.Social
{
    public interface IMenuSocialLoop
    {
        UniTask Start(IReadOnlyLifetime lifetime);
    }
}