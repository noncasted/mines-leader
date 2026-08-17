using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IUpdateProgression
    {
        UniTask Process();
    }
}