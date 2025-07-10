using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace Internal
{
    public static class LifetimeExtensions
    {
        public static async UniTask WaitInvoke(
            this IReadOnlyLifetime lifetime,
            Action<Action> subscribe,
            Action<Action> unsubscribe)
        {
            var completion =  new UniTaskCompletionSource();

            subscribe(Listener);
            lifetime.Listen(() => completion.TrySetCanceled());
            
            await completion.Task;

            unsubscribe(Listener);
            
            void Listener()
            {
                completion.TrySetResult();
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
            lifetime.Listen(child.Terminate);
            return child;
        }

        public static ILifetime Intersect(this IReadOnlyLifetime lifetimeA, IReadOnlyLifetime lifetimeB)
        {
            var child = new Lifetime();

            lifetimeA.Listen(OnTermination);
            lifetimeB.Listen(OnTermination);

            return child;

            void OnTermination()
            {
                child.Terminate();

                lifetimeA.RemoveListener(OnTermination);
                lifetimeB.RemoveListener(OnTermination);
            }
        }
    }
}