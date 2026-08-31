using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IUpdatableAction
    {
        UniTask Process();
    }
}