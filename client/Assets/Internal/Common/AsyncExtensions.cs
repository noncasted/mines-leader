using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class AsyncExtensions
    {
        public static void NoAwait(this UniTask task) => task.Forget();
        public static void NoAwait<T>(this UniTask<T> task) => task.Forget();
        public static void NoAwait(this UniTaskVoid task) => task.Forget();
    }
}
