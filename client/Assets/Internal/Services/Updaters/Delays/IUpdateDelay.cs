using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IUpdateDelay
    {
        UniTask Run();
    }
}