using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class ProfilerExtensions
    {
        /// <summary>
        /// Замер вложенной операции на конкретном скоупе, без стека: нужен там, где
        /// операции стартуют параллельно (WhenAll) и вложенность по стеку не строится.
        /// </summary>
        public static async UniTask Measure(this IProfilerScope parent, string name, UniTask task)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                await task;
            }
            finally
            {
                scope.Stop();
            }
        }

        public static async UniTask<T> Measure<T>(this IProfilerScope parent, string name, UniTask<T> task)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                return await task;
            }
            finally
            {
                scope.Stop();
            }
        }

        /// <summary>То же, но задача создаётся уже под запущенным скоупом.</summary>
        public static async UniTask Measure(this IProfilerScope parent, string name, Func<UniTask> factory)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                await factory.Invoke();
            }
            finally
            {
                scope.Stop();
            }
        }

        public static void Measure(this IProfilerScope parent, string name, Action action)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                action.Invoke();
            }
            finally
            {
                scope.Stop();
            }
        }
    }
}
