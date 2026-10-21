using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace Internal
{
    public static class LifetimeExtensions
    {
        // Completes on the first invoke of the subscribed listener, is cancelled when the lifetime ends first.
        // Both subscriptions are removed in either case.
        public static async UniTask WaitInvoke(
            this IReadOnlyLifetime lifetime,
            Action<Action> subscribe,
            Action<Action> unsubscribe)
        {
            var completion = new UniTaskCompletionSource();

            subscribe(Listener);
            lifetime.Listen(OnTerminated);

            try
            {
                await completion.Task;
            }
            finally
            {
                unsubscribe(Listener);
                lifetime.RemoveListener(OnTerminated);
            }

            void Listener()
            {
                completion.TrySetResult();
            }

            void OnTerminated()
            {
                completion.TrySetCanceled();
            }
        }

        public static void Listen<T>(this UnityEvent<T> source, IReadOnlyLifetime lifetime, UnityAction<T> listener)
        {
            source.AddListener(listener);

            lifetime.Listen(() => source.RemoveListener(listener));
        }

        public static void Listen<T>(this UnityEvent<T> source, IReadOnlyLifetime lifetime, UnityAction listener)
        {
            source.AddListener(Listener);

            lifetime.Listen(() => source.RemoveListener(Listener));

            void Listener(T value)
            {
                listener.Invoke();
            }
        }

        public static ILifetime Child(this IReadOnlyLifetime lifetime)
        {
            var child = new Lifetime(lifetime);
            lifetime.Listen(child.TerminateCallback);
            return child;
        }

        public static ILifetime Intersect(this IReadOnlyLifetime lifetimeA, IReadOnlyLifetime lifetimeB)
        {
            var child = new Lifetime();

            // Whoever ends it (either source or a direct Terminate), the subscriptions leave both sources:
            // otherwise an intersection terminated early stays on the sources until they end.
            child.Listen(() => {
                lifetimeA.RemoveListener(child.TerminateCallback);
                lifetimeB.RemoveListener(child.TerminateCallback);
            });

            lifetimeA.Listen(child.TerminateCallback);

            // A terminated source ends the intersection at once: the other one is not subscribed to at all.
            if (child.IsTerminated == true)
                return child;

            lifetimeB.Listen(child.TerminateCallback);

            return child;
        }
    }
}