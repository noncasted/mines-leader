using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class LifetimedValueExtensions
    {
        public static void View<T>(this ILifetimedValue<T> property, IReadOnlyLifetime lifetime, Action listener)
        {
            property.Advise(lifetime, (_, _) => listener.Invoke());
            listener.Invoke();
        }

        public static void View<T>(this ILifetimedValue<T> property, IReadOnlyLifetime lifetime, Action<T> listener)
        {
            property.Advise(lifetime, (_, value) => listener.Invoke(value));
            listener.Invoke(property.Value);
        }

        public static void ViewNotNull<T>(
            this ILifetimedValue<T> property,
            IReadOnlyLifetime lifetime,
            Action<T> listener) where T : class
        {
            property.Advise(lifetime, (_, value) => {
                if (value != null)
                    listener.Invoke(value);
            });

            if (property.Value != null)
                listener.Invoke(property.Value);
        }

        public static void ViewNotNull<T>(
            this ILifetimedValue<T> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, T> listener) where T : class
        {
            property.Advise(lifetime, (valueLifetime, value) => {
                if (value != null)
                    listener.Invoke(valueLifetime.Intersect(lifetime), value);
            });

            if (property.Value != null)
                listener.Invoke(property.ValueLifetime.Intersect(lifetime), property.Value);
        }


        public static void View<T>(
            this ILifetimedValue<T> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime, T> listener)
        {
            // The value lifetime given to the listener is bounded by the subscriber lifetime too: whatever the
            // listener subscribes in it ends with the subscription, not only with the next value.
            property.Advise(lifetime, (valueLifetime, value) => listener.Invoke(valueLifetime.Intersect(lifetime), value));
            listener.Invoke(property.ValueLifetime.Intersect(lifetime), property.Value);
        }

        public static void AdviseTrue(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, (_, value) => OnChange(value));

            void OnChange(bool value)
            {
                if (value == false)
                    return;

                listener?.Invoke();
            }
        }

        public static void AdviseFalse(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action listener)
        {
            property.Advise(lifetime, (_, value) => OnChange(value));

            void OnChange(bool value)
            {
                if (value == true)
                    return;

                listener?.Invoke();
            }
        }

        public static void AdviseTrue(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime> listener)
        {
            property.Advise(lifetime, OnChange);

            void OnChange(IReadOnlyLifetime valueLifetime, bool value)
            {
                if (value == false)
                    return;

                listener?.Invoke(valueLifetime.Intersect(lifetime));
            }
        }

        public static void ViewTrue(
            this ILifetimedValue<bool> property,
            IReadOnlyLifetime lifetime,
            Action<IReadOnlyLifetime> listener)
        {
            ILifetime trueLifetime = null;
            property.View(lifetime, (_, value) => OnChange(value));

            return;

            void OnChange(bool value)
            {
                if (value == false)
                {
                    trueLifetime?.Terminate();
                    return;
                }

                trueLifetime = lifetime.Child();
                listener?.Invoke(trueLifetime);
            }
        }

        public static UniTask WaitFalse(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
        {
            if (property.Value == false)
                return UniTask.CompletedTask;

            var completion = new UniTaskCompletionSource();
            lifetime.Listen(() => completion.TrySetCanceled());

            property.Advise(lifetime, (_, value) => OnChange(value));

            return completion.Task;

            void OnChange(bool value)
            {
                // The wait is over, the subscription lives on with the lifetime: a later change is not of this wait
                // (the level is not ready again between two floors).
                if (completion.Task.Status != UniTaskStatus.Pending)
                    return;

                if (value == false)
                {
                    completion.TrySetResult();
                    return;
                }

                throw new Exception();
            }
        }

        public static UniTask WaitTrue(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
        {
            if (property.Value == true)
                return UniTask.CompletedTask;

            var completion = new UniTaskCompletionSource();
            lifetime.Listen(() => completion.TrySetCanceled());

            property.Advise(lifetime, (_, value) => OnChange(value));

            return completion.Task;

            void OnChange(bool value)
            {
                // The wait is over, the subscription lives on with the lifetime: a later change is not of this wait
                // (the level is not ready again between two floors).
                if (completion.Task.Status != UniTaskStatus.Pending)
                    return;

                if (value == true)
                {
                    completion.TrySetResult();
                    return;
                }

                throw new Exception();
            }
        }
    }
}